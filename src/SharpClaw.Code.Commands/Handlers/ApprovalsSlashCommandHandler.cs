using SharpClaw.Code.Commands.Models;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Provides a durable alias over the permissions auto-approval subcommands.
/// </summary>
public sealed class ApprovalsSlashCommandHandler(PermissionsCommandHandler permissionsCommandHandler) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "approvals";

    /// <inheritdoc />
    public string Description => "Alias for /permissions approvals show|set|clear.";

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
        => permissionsCommandHandler.ExecuteAsync(
            new SlashCommandParseResult(
                true,
                "permissions",
                command.Arguments.Length == 0
                    ? ["approvals", "show"]
                    : ["approvals", .. command.Arguments]),
            context,
            cancellationToken);
}
