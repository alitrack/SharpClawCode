using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Protocol.Commands;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Clears the REPL screen and transient overrides without touching durable session state.
/// </summary>
public sealed class ClearSlashCommandHandler(
    ReplInteractionState replInteractionState,
    IReplTerminal terminal,
    OutputRendererDispatcher outputRendererDispatcher) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "clear";

    /// <inheritdoc />
    public string Description => "Clears the REPL screen and transient overrides.";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        replInteractionState.ClearTransientOverrides();
        terminal.ClearScreen();
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, "Cleared the REPL screen and transient overrides.", null),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
