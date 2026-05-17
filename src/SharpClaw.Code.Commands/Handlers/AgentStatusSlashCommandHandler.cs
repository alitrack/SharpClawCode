using SharpClaw.Code.Commands.Models;

namespace SharpClaw.Code.Commands;

/// <summary>
/// REPL alias for external agent status.
/// </summary>
public sealed class AgentStatusSlashCommandHandler(AgentsCommandHandler agentsCommandHandler) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "agent-status";

    /// <inheritdoc />
    public string Description => "Shows external agent adapter status.";

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
        => agentsCommandHandler.ExecuteAsync(new SlashCommandParseResult(true, "agents", ["external", "status"]), context, cancellationToken);
}
