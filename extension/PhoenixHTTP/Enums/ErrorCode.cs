namespace PhoenixHttp.Enums;

/// <summary>
/// Failure reasons reported to the script in place of an HTTP status code. The values are negative
/// on purpose so the script can tell a transport failure apart from any real HTTP status, which is
/// always positive.
/// </summary>
public enum ErrorCode
{
    /// <summary>The host could not be reached (DNS, connection refused, network down).</summary>
    NetworkUnreachable = -1,

    /// <summary>The request exceeded the configured timeout.</summary>
    Timeout = -2,

    /// <summary>The request could not be built from the supplied parameters.</summary>
    InvalidRequest = -3,

    /// <summary>The response could not be serialized for the script.</summary>
    SerializationFailed = -4,

    /// <summary>An unclassified failure; see the log for the underlying exception.</summary>
    Unknown = -100
}
