using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Sessions.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Shows the latest checkpoint for the active session.
/// </summary>
public sealed class CheckpointsSlashCommandHandler(
    ICheckpointStore checkpointStore,
    ISessionStore sessionStore,
    OutputRendererDispatcher outputRendererDispatcher) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "checkpoints";

    /// <inheritdoc />
    public string Description => "Shows the latest runtime checkpoint.";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var session = string.IsNullOrWhiteSpace(context.SessionId)
            ? await sessionStore.GetLatestAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false)
            : await sessionStore.GetByIdAsync(context.WorkingDirectory, context.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            await outputRendererDispatcher.RenderCommandResultAsync(new CommandResult(false, 1, context.OutputFormat, "No session found.", null), context.OutputFormat, cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var checkpoint = await checkpointStore.GetLatestAsync(context.WorkingDirectory, session.Id, cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(
                checkpoint is not null,
                checkpoint is null ? 1 : 0,
                context.OutputFormat,
                checkpoint is null ? "No checkpoint found." : $"Latest checkpoint {checkpoint.Id}.",
                checkpoint is null ? null : JsonSerializer.Serialize(checkpoint, ProtocolJsonContext.Default.RuntimeCheckpoint)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return checkpoint is null ? 1 : 0;
    }
}
