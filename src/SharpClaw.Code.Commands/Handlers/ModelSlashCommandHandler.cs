using SharpClaw.Code.Commands.Models;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Provides a singular alias over the models slash command surface.
/// </summary>
public sealed class ModelSlashCommandHandler(ModelsCommandHandler modelsCommandHandler) : ISlashCommandHandler
{
    /// <inheritdoc />
    public string CommandName => "model";

    /// <inheritdoc />
    public string Description => "Alias for /models show|use|clear.";

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
        => modelsCommandHandler.ExecuteAsync(
            new SlashCommandParseResult(
                true,
                "models",
                command.Arguments.Length == 0 ? ["show"] : command.Arguments),
            context,
            cancellationToken);
}
