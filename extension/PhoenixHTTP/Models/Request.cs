using System.Text;

namespace PhoenixHttp.Models;

/// <summary>
/// The outgoing HTTP request as it is assembled from the script. Method and URL are fixed at
/// creation; headers, query parameters and the body are filled in afterwards by successive
/// <c>request:*</c> verbs, which is why those are mutable collections rather than init-only values.
/// The body is a <see cref="StringBuilder"/> because it arrives in chunks and is concatenated.
/// </summary>
public sealed class Request
{
    /// <summary>HTTP method, already upper-cased (for example <c>GET</c> or <c>POST</c>).</summary>
    public required string Method { get; init; }

    /// <summary>Target URL, before query parameters and <c>{{macro}}</c> expansion are applied.</summary>
    public required string Url { get; init; }

    /// <summary>Request headers, keyed by name; later assignments overwrite earlier ones.</summary>
    public Dictionary<string, string> Headers { get; } = new();

    /// <summary>Query-string parameters, keyed by name; appended to the URL at send time.</summary>
    public Dictionary<string, string> Query { get; } = new();

    /// <summary>Request body accumulated from the chunks supplied by the <c>request:body</c> verb.</summary>
    public StringBuilder Body { get; } = new();
}
