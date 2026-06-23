namespace PhoenixHttp.Http;

/// <summary>The raw outcome of one HTTP exchange, before it is shaped into a script-facing response.</summary>
/// <param name="StatusCode">The HTTP status code returned by the server.</param>
/// <param name="Headers">Response headers, with lower-cased names and comma-joined values.</param>
/// <param name="Body">The response body decoded as a string.</param>
public sealed record HttpExecution(int StatusCode, Dictionary<string, string> Headers, string Body);
