using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Top-level command surface for external agent adapters.
/// </summary>
public sealed class ExternalCommandHandler(
    IExternalAgentService externalAgentService,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler
{
    /// <inheritdoc />
    public string Name => "external";

    /// <inheritdoc />
    public string Description => "Lists and runs external agent adapters.";

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        var list = new Command("list", "Lists external agent adapters.");
        list.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(list);

        var status = new Command("status", "Shows external agent adapter health.");
        status.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(status);

        var run = new Command("run", "Runs a prompt through an external agent.");
        var adapter = new Option<string>("--adapter") { Required = true, Description = "Adapter id." };
        var prompt = new Option<string>("--prompt") { Required = true, Description = "Prompt text." };
        var mode = new Option<string>("--mode") { DefaultValueFactory = _ => "workspaceWrite", Description = "readOnly or workspaceWrite." };
        run.Options.Add(adapter);
        run.Options.Add(prompt);
        run.Options.Add(mode);
        run.SetAction((parseResult, cancellationToken) => ExecuteRunAsync(
            parseResult.GetValue(adapter)!,
            parseResult.GetValue(prompt)!,
            ParseMode(parseResult.GetValue(mode)),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        command.Subcommands.Add(run);
        command.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private async Task<int> ExecuteListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var report = await externalAgentService.ListAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        var available = report.Agents.Count(agent => agent.Health == ExternalAgentHealth.Available);
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                $"External agents {(report.Enabled ? "enabled" : "disabled")}. {available}/{report.Agents.Count} adapter(s) available.",
                JsonSerializer.Serialize(report, ProtocolJsonContext.Default.ExternalAgentCatalogReport)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteRunAsync(
        string adapterId,
        string prompt,
        ExternalAgentMode mode,
        CommandExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = await externalAgentService.RunAsync(
            new ExternalAgentRunRequest(
                adapterId,
                context.WorkingDirectory,
                prompt,
                mode,
                context.SessionId,
                PermissionMode: context.PermissionMode,
                PrimaryMode: context.PrimaryMode,
                IsInteractive: context.OutputFormat == OutputFormat.Text),
            cancellationToken).ConfigureAwait(false);
        var success = result.FailureKind == ExternalAgentFailureKind.None;
        return await RenderAsync(
            new CommandResult(
                success,
                success ? 0 : 1,
                context.OutputFormat,
                success ? result.OutputText : result.Error ?? "External agent run failed.",
                JsonSerializer.Serialize(result, ProtocolJsonContext.Default.ExternalAgentRunResult)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RenderAsync(CommandResult result, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private static ExternalAgentMode ParseMode(string? value)
    {
        if (string.Equals(value, "readOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "read", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalAgentMode.ReadOnly;
        }

        if (string.Equals(value, "workspaceWrite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "write", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "writeOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalAgentMode.WorkspaceWrite;
        }

        throw new ArgumentException($"Unsupported external agent mode '{value}'. Expected readOnly or workspaceWrite.", nameof(value));
    }
}
