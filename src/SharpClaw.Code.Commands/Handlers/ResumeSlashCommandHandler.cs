using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Attaches an existing session by id and clears transient REPL overrides.
/// </summary>
public sealed class ResumeSlashCommandHandler(
    IRuntimeCommandService runtimeCommandService,
    ReplInteractionState replInteractionState,
    OutputRendererDispatcher outputRendererDispatcher) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "resume";

    /// <inheritdoc />
    public string Description => "Alias for /session attach <sessionId>.";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length == 0)
        {
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(false, 1, context.OutputFormat, "Usage: /resume <sessionId>", null),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        replInteractionState.ClearTransientOverrides();
        var result = await runtimeCommandService
            .AttachSessionAsync(command.Arguments[0], context.ToRuntimeCommandContext(), cancellationToken)
            .ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
