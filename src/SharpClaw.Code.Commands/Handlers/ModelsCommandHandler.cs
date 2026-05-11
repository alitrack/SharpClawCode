using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Providers.Abstractions;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Lists the configured provider/model surface available to SharpClaw.
/// </summary>
public sealed class ModelsCommandHandler(
    IProviderCatalogService providerCatalogService,
    ISessionPreferenceService sessionPreferenceService,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "models";

    /// <inheritdoc />
    public string Description => "Lists provider defaults, aliases, and authentication status.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        var show = new Command("show", "Shows the active session model preference.");
        show.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(show);

        var use = new Command("use", "Persists a session-scoped model preference.");
        var modelArgument = new Argument<string>("model")
        {
            Description = "Provider/model id or configured alias."
        };
        use.Arguments.Add(modelArgument);
        use.SetAction((parseResult, cancellationToken) => ExecuteUseAsync(
            parseResult.GetValue(modelArgument) ?? throw new InvalidOperationException("model is required."),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        command.Subcommands.Add(use);

        var clear = new Command("clear", "Clears the persisted session model preference.");
        clear.SetAction((parseResult, cancellationToken) => ExecuteClearAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(clear);

        command.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length == 0 || string.Equals(command.Arguments[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteListAsync(context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteShowAsync(context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "clear", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.Arguments[0], "reset", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteClearAsync(context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "use", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2)
        {
            return ExecuteUseAsync(command.Arguments[1], context, cancellationToken);
        }

        return RenderAsync("Usage: /models [list|show|use <provider/model|alias>|clear]", context, cancellationToken, success: false);
    }

    private async Task<int> ExecuteListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var entries = await providerCatalogService.ListAsync(cancellationToken).ConfigureAwait(false);
        var payload = entries.ToList();

        var result = new CommandResult(
            true,
            0,
            context.OutputFormat,
            $"{payload.Count} provider model surface(s).",
            JsonSerializer.Serialize(payload, ProtocolJsonContext.Default.ListProviderModelCatalogEntry));
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteShowAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var report = await sessionPreferenceService
            .GetPermissionStatusAsync(
                context.WorkingDirectory,
                context.SessionId,
                context.PermissionMode,
                context.ApprovalSettings,
                context.Model,
                cancellationToken)
            .ConfigureAwait(false);
        var message = string.IsNullOrWhiteSpace(report.EffectiveModel)
            ? "No session model preference is currently persisted."
            : $"Active session model preference: {report.EffectiveModel}.";
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                message,
                JsonSerializer.Serialize(report, ProtocolJsonContext.Default.PermissionStatusReport)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUseAsync(string model, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var preference = await sessionPreferenceService
            .SetModelPreferenceAsync(context.WorkingDirectory, context.SessionId, model, cancellationToken)
            .ConfigureAwait(false);
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                $"Persisted session model preference '{preference.Model}'.",
                JsonSerializer.Serialize(preference, ProtocolJsonContext.Default.SessionModelPreference)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteClearAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var removed = await sessionPreferenceService
            .ClearModelPreferenceAsync(context.WorkingDirectory, context.SessionId, cancellationToken)
            .ConfigureAwait(false);
        return await RenderAsync(
            new CommandResult(
                removed,
                removed ? 0 : 1,
                context.OutputFormat,
                removed ? "Cleared the persisted session model preference." : "No persisted session model preference was found.",
                null),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RenderAsync(CommandResult result, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private Task<int> RenderAsync(string message, CommandExecutionContext context, CancellationToken cancellationToken, bool success = true)
        => RenderAsync(new CommandResult(success, success ? 0 : 1, context.OutputFormat, message, null), context, cancellationToken);
}
