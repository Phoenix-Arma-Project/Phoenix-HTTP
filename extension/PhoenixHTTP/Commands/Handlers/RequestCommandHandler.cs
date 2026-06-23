using PhoenixHttp.Arma;
using PhoenixHttp.Enums;
using PhoenixHttp.Http;
using PhoenixHttp.Models;

namespace PhoenixHttp.Commands.Handlers;

/// <summary>
/// Handles the <c>request:*</c> verbs. A request is built up incrementally across several calls
/// because <c>callExtension</c> is synchronous with a capped reply size: the script creates a
/// transaction, layers on headers, query parameters and body chunks, then sends it. The response is
/// later pulled back out chunk by chunk. Every handler therefore works against a stored transaction
/// identified by its id.
/// </summary>
public static class RequestCommandHandler
{
    /// <summary>
    /// Creates a new transaction for a method and URL and returns its id, which the script uses to
    /// reference the request in every subsequent verb.
    /// </summary>
    /// <param name="arguments">[method, url].</param>
    /// <returns>A success reply carrying the new transaction id, or an error reply.</returns>
    public static string Create(string[] arguments)
    {
        if (arguments.Length < 2)
        {
            return Reply.Error("create expects method and url");
        }

        string method = arguments[0].ToUpperInvariant();
        string id = Guid.NewGuid().ToString();
        Extension.Store.Add(new Transaction
        {
            Id = id,
            Request = new Request
            {
                Method = method,
                Url = arguments[1]
            }
        });

        Extension.Logger.Debug($"{id} created {method} {arguments[1]}");
        return Reply.Success(id);
    }

    /// <summary>Adds or overwrites a request header on an existing transaction.</summary>
    /// <param name="arguments">[id, key, value].</param>
    /// <returns>An empty success reply, or an error reply.</returns>
    public static string Header(string[] arguments)
    {
        if (arguments.Length < 3)
        {
            return Reply.Error("header expects id, key and value");
        }

        if (!Extension.Store.TryGet(arguments[0], out Transaction transaction))
        {
            return Reply.Error($"unknown transaction '{arguments[0]}'");
        }

        transaction.Request.Headers[arguments[1]] = arguments[2];
        return Reply.Success(string.Empty);
    }

    /// <summary>Adds or overwrites a query-string parameter on an existing transaction.</summary>
    /// <param name="arguments">[id, key, value].</param>
    /// <returns>An empty success reply, or an error reply.</returns>
    public static string Query(string[] arguments)
    {
        if (arguments.Length < 3)
        {
            return Reply.Error("query expects id, key and value");
        }

        if (!Extension.Store.TryGet(arguments[0], out Transaction transaction))
        {
            return Reply.Error($"unknown transaction '{arguments[0]}'");
        }

        transaction.Request.Query[arguments[1]] = arguments[2];
        return Reply.Success(string.Empty);
    }

    /// <summary>
    /// Appends one chunk to the request body. Large bodies are split by the script because a single
    /// <c>callExtension</c> argument is length-limited, so this verb may run many times per request.
    /// </summary>
    /// <param name="arguments">[id, chunk].</param>
    /// <returns>An empty success reply, or an error reply.</returns>
    public static string Body(string[] arguments)
    {
        if (arguments.Length < 2)
        {
            return Reply.Error("body expects id and a chunk");
        }

        if (!Extension.Store.TryGet(arguments[0], out Transaction transaction))
        {
            return Reply.Error($"unknown transaction '{arguments[0]}'");
        }

        transaction.Request.Body.Append(arguments[1]);
        return Reply.Success(string.Empty);
    }

    /// <summary>
    /// Queues the fully built request for asynchronous execution. The call returns immediately; the
    /// result is delivered later through the engine callback when a response is requested.
    /// </summary>
    /// <param name="arguments">[id, parser ("json" or "raw"), needsResponse ("true"/"1" or else)].</param>
    /// <returns>A success reply carrying the transaction id, or an error reply.</returns>
    public static string Send(string[] arguments)
    {
        if (arguments.Length < 3)
        {
            return Reply.Error("send expects id, parser and a response flag");
        }

        if (!Extension.Store.TryGet(arguments[0], out Transaction transaction))
        {
            return Reply.Error($"unknown transaction '{arguments[0]}'");
        }

        ResponseParser parser = string.Equals(arguments[1], "json", StringComparison.OrdinalIgnoreCase)
            ? ResponseParser.Json
            : ResponseParser.Raw;
        bool needsResponse = string.Equals(arguments[2], "true", StringComparison.OrdinalIgnoreCase) || arguments[2] == "1";

        transaction.Status = TransactionStatus.Processing;
        transaction.SentAt = DateTime.UtcNow;
        Extension.Queue.Enqueue(() => RequestExecutor.ExecuteAsync(transaction, parser, needsResponse));

        Extension.Logger.Debug($"{transaction.Id} queued (parser={parser}, needsResponse={needsResponse})");
        return Reply.Success(transaction.Id);
    }

    /// <summary>
    /// Returns one chunk of a completed response. The script reads chunks in order and reassembles
    /// them, which is how a response larger than the reply buffer crosses the boundary.
    /// </summary>
    /// <param name="arguments">[id, chunkIndex].</param>
    /// <returns>A success reply carrying the requested chunk, or an error reply.</returns>
    public static string Get(string[] arguments)
    {
        if (arguments.Length < 2)
        {
            return Reply.Error("get expects id and a chunk index");
        }

        if (!Extension.Store.TryGet(arguments[0], out Transaction transaction))
        {
            return Reply.Error($"unknown transaction '{arguments[0]}'");
        }

        if (transaction.ResponseChunks is not { } chunks)
        {
            return Reply.Error("response is not ready");
        }

        if (!int.TryParse(arguments[1], out int chunkIndex) || chunkIndex < 0 || chunkIndex >= chunks.Count)
        {
            return Reply.Error($"chunk index must be between 0 and {chunks.Count - 1}");
        }

        return Reply.Success(chunks[chunkIndex]);
    }

    /// <summary>Drops a single transaction, normally after the script has read its response.</summary>
    /// <param name="arguments">[id].</param>
    /// <returns>An empty success reply, or an error reply.</returns>
    public static string Delete(string[] arguments)
    {
        if (arguments.Length < 1)
        {
            return Reply.Error("delete expects an id");
        }

        Extension.Store.Remove(arguments[0]);
        return Reply.Success(string.Empty);
    }

    /// <summary>Drops every transaction, for example on mission restart.</summary>
    /// <param name="arguments">Unused; the verb takes no arguments.</param>
    /// <returns>An empty success reply.</returns>
    public static string Clear(string[] arguments)
    {
        Extension.Store.Clear();
        return Reply.Success(string.Empty);
    }
}
