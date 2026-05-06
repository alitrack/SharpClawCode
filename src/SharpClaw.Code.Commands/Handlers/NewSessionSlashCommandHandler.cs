using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Creates and attaches a fresh session for the current workspace.
/// </summary>
public sealed class NewSessionSlashCommandHandler(
    IConversationRuntime conversationRuntime,
    IRuntimeCommandService runtimeCommandService,
    ReplInteractionState replInteractionState,
    OutputRendererDispatcher outputRendererDispatcher) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "new";

    /// <inheritdoc />
    public string Description => "Creates and attaches a fresh workspace session.";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var session = await conversationRuntime
            .CreateSessionAsync(
                context.WorkingDirectory,
                replInteractionState.PermissionModeOverride ?? context.PermissionMode,
                context.OutputFormat,
                cancellationToken)
            .ConfigureAwait(false);
        replInteractionState.ClearTransientOverrides();
        await runtimeCommandService
            .AttachSessionAsync(session.Id, context.ToRuntimeCommandContext(), cancellationToken)
            .ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                $"Created and attached session '{session.Id}'.",
                JsonSerializer.Serialize(session, ProtocolJsonContext.Default.ConversationSession)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
