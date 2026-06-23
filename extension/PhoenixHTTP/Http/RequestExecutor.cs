using System.Net.Http;
using PhoenixHttp.Arma;
using PhoenixHttp.Enums;
using PhoenixHttp.Models;
using PhoenixHttp.Utils;

namespace PhoenixHttp.Http;

/// <summary>
/// Runs a queued transaction end to end: expands macros, decides how to encode the body, performs
/// the HTTP exchange, and turns the outcome into the script-facing response. It owns the bridge
/// between the failure-prone network call and the engine, so it never lets an exception escape.
/// </summary>
public static class RequestExecutor
{
    /// <summary>
    /// Executes one transaction and stores or discards its result. Any failure is converted into an
    /// error response so the script always sees a well-formed result; the completion step is itself
    /// guarded so a serialization fault cannot leave the script waiting forever.
    /// </summary>
    /// <param name="transaction">The transaction to run.</param>
    /// <param name="parser">How to shape the response body.</param>
    /// <param name="needsResponse">Whether the script is waiting to read the response.</param>
    public static async Task ExecuteAsync(Transaction transaction, ResponseParser parser, bool needsResponse)
    {
        Request request = transaction.Request;
        Extension.Logger.Debug($"{transaction.Id} executing {request.Method} {request.Url}");

        try
        {
            HttpExecution execution = await SendAsync(request);
            double elapsed = (DateTime.UtcNow - (transaction.SentAt ?? DateTime.UtcNow)).TotalMilliseconds;
            Extension.Logger.Information($"{transaction.Id} {request.Method} {request.Url} -> {execution.StatusCode} in {elapsed:F0}ms");
            Complete(transaction, BuildResponse(execution, parser), needsResponse);
        }
        catch (Exception exception)
        {
            Extension.Logger.Error($"{transaction.Id} {request.Method} {request.Url} failed: {exception.Message}");
            Extension.Logger.Debug($"{transaction.Id} failure detail: {exception}");
            try
            {
                Complete(transaction, BuildError(exception), needsResponse);
            }
            catch (Exception completeException)
            {
                Extension.Logger.Error($"{transaction.Id} could not be completed: {completeException}");
            }
        }
    }

    /// <summary>
    /// Expands <c>{{macro}}</c> tokens throughout the request and chooses the body encoding, then
    /// hands off to the HTTP client. A body that the SQF wrapper serialized from a hashmap arrives as
    /// a nested pair array (its text starts with <c>[[</c>); that is decoded and re-emitted as JSON.
    /// Any other non-empty body is sent verbatim with the caller's own content type.
    /// </summary>
    /// <param name="request">The assembled request.</param>
    /// <returns>The HTTP exchange result.</returns>
    private static Task<HttpExecution> SendAsync(Request request)
    {
        IReadOnlyDictionary<string, string> environments = Extension.Config.Environments;

        string url = MacroProcessor.Apply(request.Url, environments);

        Dictionary<string, string> headers = new();
        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            headers[header.Key] = MacroProcessor.Apply(header.Value, environments);
        }

        Dictionary<string, string> query = new();
        foreach (KeyValuePair<string, string> parameter in request.Query)
        {
            query[parameter.Key] = MacroProcessor.Apply(parameter.Value, environments);
        }

        string rawBody = MacroProcessor.Apply(request.Body.ToString(), environments);
        string? body;
        string? contentType;

        if (rawBody.Length == 0)
        {
            body = null;
            contentType = null;
        }
        else if (rawBody.TrimStart().StartsWith("[[", StringComparison.Ordinal))
        {
            // A hashmap body the wrapper serialized into the SQF pair format: decode it and send JSON.
            body = JsonWriter.Write(Deserializer.Deserialize(rawBody));
            contentType = "application/json";
        }
        else
        {
            // A plain string body: send it as-is, honoring any content type the caller set.
            body = rawBody;
            contentType = TakeContentType(headers);
        }

        TimeSpan timeout = TimeSpan.FromSeconds(Extension.Config.RequestTimeoutSeconds);
        return Extension.Client.SendAsync(request.Method, url, query, headers, body, contentType, timeout);
    }

    /// <summary>
    /// Finalizes a transaction. When no response is wanted the transaction is dropped immediately;
    /// otherwise the response is serialized for SQF, split into chunks, and the engine is signalled
    /// so the script can pull the chunks back. The script removes the transaction once it has them.
    /// </summary>
    /// <param name="transaction">The transaction to finalize.</param>
    /// <param name="response">The response shaped for the script.</param>
    /// <param name="needsResponse">Whether the script is waiting to read the response.</param>
    private static void Complete(Transaction transaction, Dictionary<string, object?> response, bool needsResponse)
    {
        if (!needsResponse)
        {
            Extension.Store.Remove(transaction.Id);
            return;
        }

        IReadOnlyList<string> chunks = Chunker.Split(Serializer.Serialize(response), Extension.Config.ChunkSize);
        transaction.ResponseChunks = chunks;
        transaction.Status = TransactionStatus.Done;
        transaction.CompletedAt = DateTime.UtcNow;

        Extension.Logger.Debug($"{transaction.Id} response ready in {chunks.Count} chunk(s)");
        Extension.Invoke(Extension.CallbackName, "response", $"[\"{transaction.Id}\",{chunks.Count}]");
    }

    /// <summary>
    /// Shapes a successful exchange into the response map the script receives. With the JSON parser
    /// the body is parsed into a navigable structure.
    /// </summary>
    /// <param name="execution">The raw HTTP outcome.</param>
    /// <param name="parser">How to shape the body.</param>
    /// <returns>The response map: success flag, status code, headers and body.</returns>
    private static Dictionary<string, object?> BuildResponse(HttpExecution execution, ResponseParser parser)
    {
        object? body = execution.Body;
        if (parser == ResponseParser.Json && execution.Body.Length > 0)
        {
            body = JsonParser.Parse(execution.Body);
        }

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["status_code"] = execution.StatusCode,
            ["headers"] = execution.Headers,
            ["body"] = body
        };
    }

    /// <summary>
    /// Shapes a failed exchange into a response map whose <c>status_code</c> is a negative
    /// <see cref="ErrorCode"/>, so the script can distinguish a transport failure from an HTTP status.
    /// </summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns>The error response map.</returns>
    private static Dictionary<string, object?> BuildError(Exception exception)
    {
        ErrorCode code = exception switch
        {
            TaskCanceledException => ErrorCode.Timeout,
            HttpRequestException => ErrorCode.NetworkUnreachable,
            System.Text.Json.JsonException => ErrorCode.SerializationFailed,
            _ => ErrorCode.Unknown
        };

        return new Dictionary<string, object?>
        {
            ["success"] = false,
            ["status_code"] = (int)code,
            ["headers"] = new Dictionary<string, string>(),
            ["body"] = null
        };
    }

    /// <summary>
    /// Removes and returns the content-type header (matched case-insensitively) so it can be applied
    /// to the body content rather than sent as an ordinary request header, which HttpClient forbids.
    /// </summary>
    /// <param name="headers">The request headers; the content-type entry is removed if present.</param>
    /// <returns>The content-type value, or null when none was set.</returns>
    private static string? TakeContentType(Dictionary<string, string> headers)
    {
        string? key = headers.Keys.FirstOrDefault(name => string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return null;
        }

        string value = headers[key];
        headers.Remove(key);
        return value;
    }
}
