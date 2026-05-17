using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Shows a static runtime workbench view.
/// </summary>
public sealed class WorkbenchCommandHandler(
    IWorkbenchStatusService workbenchStatusService,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "workbench";

    /// <inheritdoc />
    public string Description => "Shows sessions, approvals, checkpoints, tool activity, and adapter health.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(new SlashCommandParseResult(true, Name, []), globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var report = await workbenchStatusService.BuildAsync(context.ToRuntimeCommandContext(), cancellationToken).ConfigureAwait(false);
        var message = $"Session {report.CurrentSessionId ?? "none"} · mode {report.PrimaryMode} · agent {report.ActiveAgentId ?? "default"} · checkpoint {report.LatestCheckpoint?.Id ?? "none"} · external {report.ExternalAgents.Count(agent => agent.Health == Protocol.Models.ExternalAgentHealth.Available)}/{report.ExternalAgents.Count}";
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, message, JsonSerializer.Serialize(report, ProtocolJsonContext.Default.WorkbenchStatusReport)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
