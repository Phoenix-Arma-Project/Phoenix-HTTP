using PhoenixHttp.Arma;

namespace PhoenixHttp.Commands.Handlers;

/// <summary>Handles the <c>config:*</c> verbs that operate on the extension's configuration.</summary>
public static class ConfigCommandHandler
{
    /// <summary>Re-reads <c>config.json</c> at runtime so operators can tune the server live.</summary>
    /// <param name="arguments">Unused; the verb takes no arguments.</param>
    /// <returns>An empty success reply.</returns>
    public static string Reload(string[] arguments)
    {
        Extension.Reload();
        return Reply.Success(string.Empty);
    }
}
