using PhoenixHttp.Arma;
using PhoenixHttp.Commands.Handlers;

namespace PhoenixHttp.Commands;

/// <summary>
/// Translates a command verb received over <c>callExtension</c> into the handler that implements it.
/// The verb table is the extension's public API: each entry is one operation the SQF wrapper can
/// invoke, and the names are part of the contract shared with the addon.
/// </summary>
public sealed class Router
{
    /// <summary>Verb-to-handler table, built once and read-only thereafter.</summary>
    private readonly Dictionary<string, Func<string[], string>> routes;

    /// <summary>Builds the verb table that maps every supported command to its handler.</summary>
    public Router()
    {
        routes = new Dictionary<string, Func<string[], string>>
        {
            ["request:create"] = RequestCommandHandler.Create,
            ["request:header"] = RequestCommandHandler.Header,
            ["request:query"] = RequestCommandHandler.Query,
            ["request:body"] = RequestCommandHandler.Body,
            ["request:send"] = RequestCommandHandler.Send,
            ["request:get"] = RequestCommandHandler.Get,
            ["request:delete"] = RequestCommandHandler.Delete,
            ["request:clear"] = RequestCommandHandler.Clear,
            ["config:reload"] = ConfigCommandHandler.Reload
        };
    }

    /// <summary>Runs the handler for a command, or returns an error reply for an unknown verb.</summary>
    /// <param name="command">The command verb to dispatch.</param>
    /// <param name="arguments">The decoded command arguments.</param>
    /// <returns>The handler's reply, or an error reply if the verb is not registered.</returns>
    public string Execute(string command, string[] arguments)
    {
        if (!routes.TryGetValue(command, out Func<string[], string>? handler))
        {
            return Reply.Error($"unknown command '{command}'");
        }

        return handler(arguments);
    }
}
