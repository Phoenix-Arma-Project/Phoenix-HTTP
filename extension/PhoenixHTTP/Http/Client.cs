using System.Net.Http.Headers;
using System.Text;

namespace PhoenixHttp.Http;

/// <summary>
/// Thin wrapper over a single shared <see cref="System.Net.Http.HttpClient"/>. One client is reused
/// for the whole process so sockets are pooled. Its built-in timeout is disabled and the per-request
/// timeout is enforced with a <see cref="CancellationTokenSource"/> instead, because each request
/// carries its own deadline from the (reloadable) config.
/// </summary>
public sealed class Client
{
    /// <summary>The pooled client; timeout is per request, not on the client.</summary>
    private readonly System.Net.Http.HttpClient httpClient;

    /// <summary>Creates the shared client with its global timeout disabled.</summary>
    public Client()
    {
        httpClient = new System.Net.Http.HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>Builds and sends one HTTP request and reads the full response.</summary>
    /// <param name="method">HTTP method, for example <c>GET</c>.</param>
    /// <param name="url">Target URL, before query parameters are appended.</param>
    /// <param name="query">Query parameters to append to the URL.</param>
    /// <param name="headers">Request headers to send.</param>
    /// <param name="body">Request body, or null for no body.</param>
    /// <param name="contentType">Content type for the body, or null to leave it unset.</param>
    /// <param name="timeout">Deadline for the whole exchange, including reading the body.</param>
    /// <returns>The status code, headers and body of the response.</returns>
    public async Task<HttpExecution> SendAsync(
        string method,
        string url,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        string? contentType,
        TimeSpan timeout)
    {
        using HttpRequestMessage message = new(new HttpMethod(method), BuildUrl(url, query));

        if (body is not null)
        {
            message.Content = new StringContent(body, Encoding.UTF8);
            if (contentType is not null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        // TryAddWithoutValidation so request headers and content headers alike are accepted as-is,
        // rather than being rejected by HttpClient's stricter typed-header rules.
        foreach (KeyValuePair<string, string> header in headers)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using CancellationTokenSource cancellation = new(timeout);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellation.Token);
        string responseBody = await response.Content.ReadAsStringAsync(cancellation.Token);

        return new HttpExecution((int)response.StatusCode, ReadHeaders(response), responseBody);
    }

    /// <summary>Appends the query parameters to the URL, URL-encoding each key and value.</summary>
    /// <param name="url">Base URL, which may already contain a query string.</param>
    /// <param name="query">Parameters to append.</param>
    /// <returns>The URL with the parameters appended, or the original URL when there are none.</returns>
    private static string BuildUrl(string url, IReadOnlyDictionary<string, string> query)
    {
        if (query.Count == 0)
        {
            return url;
        }

        StringBuilder builder = new(url);
        builder.Append(url.Contains('?') ? '&' : '?');

        bool first = true;
        foreach (KeyValuePair<string, string> parameter in query)
        {
            if (!first)
            {
                builder.Append('&');
            }

            first = false;
            builder.Append(Uri.EscapeDataString(parameter.Key)).Append('=').Append(Uri.EscapeDataString(parameter.Value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Flattens both the response and content headers into one dictionary, lower-casing names so the
    /// script can look them up predictably and joining multi-valued headers with a comma.
    /// </summary>
    /// <param name="response">The response to read headers from.</param>
    /// <returns>The merged headers.</returns>
    private static Dictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        Dictionary<string, string> headers = new();
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
        {
            headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
        {
            headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
        }

        return headers;
    }
}
