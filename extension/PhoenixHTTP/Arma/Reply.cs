namespace PhoenixHttp.Arma;

/// <summary>
/// Builds the <c>status:payload</c> reply strings every command returns. The first colon separates
/// the status from the payload; the SQF wrapper splits on exactly that first colon, so payloads are
/// free to contain colons of their own.
/// </summary>
public static class Reply
{
    /// <summary>Builds a success reply carrying the given payload.</summary>
    /// <param name="data">The payload, which may be empty or contain colons.</param>
    /// <returns>The <c>success:data</c> reply string.</returns>
    public static string Success(string data) => $"success:{data}";

    /// <summary>Builds an error reply carrying a human-readable message.</summary>
    /// <param name="message">The error description.</param>
    /// <returns>The <c>error:message</c> reply string.</returns>
    public static string Error(string message) => $"error:{message}";
}
