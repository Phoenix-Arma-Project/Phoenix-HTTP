namespace PhoenixHttp.Enums;

/// <summary>How the response body should be handed back to the script.</summary>
public enum ResponseParser
{
    /// <summary>Return the body verbatim as a single string.</summary>
    Raw,

    /// <summary>Parse the body as JSON into a structure the script can navigate.</summary>
    Json
}
