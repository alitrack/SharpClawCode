using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Lists the effective agent catalog and manages the REPL agent override.
/// </summary>
public sealed class AgentsCommandHandler(
    IAgentCatalogService agentCatalogService,
    IExternalAgentService externalAgentService,
    ReplInteractionState replInteractionState,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "agents";

    /// <inheritdoc />
    public string Description => "Lists agents and selects the active REPL agent override.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);

        var list = new Command("list", "Lists the effective agent catalog.");
        list.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(list);

        var use = new Command("use", "Sets the current process REPL agent override.");
        var idOption = new Option<string>("--id") { Required = true, Description = "Agent id to activate." };
        use.Options.Add(idOption);
        use.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = globalOptions.Resolve(parseResult);
            var id = parseResult.GetValue(idOption) ?? throw new InvalidOperationException("--id is required.");
            return await ExecuteUseAsync(id, context, cancellationToken).ConfigureAwait(false);
        });
        command.Subcommands.Add(use);

        var external = new Command("external", "Lists, checks, and runs external agent adapters.");
        external.Subcommands.Add(BuildExternalListCommand(globalOptions));
        external.Subcommands.Add(BuildExternalStatusCommand(globalOptions));
        external.Subcommands.Add(BuildExternalRunCommand(globalOptions));
        external.SetAction((parseResult, cancellationToken) => ExecuteExternalListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(external);

        command.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length >= 2 && string.Equals(command.Arguments[0], "use", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteUseAsync(command.Arguments[1], context, cancellationToken);
        }

        if (command.Arguments.Length >= 1 && string.Equals(command.Arguments[0], "external", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Arguments.Length == 1 || string.Equals(command.Arguments[1], "list", StringComparison.OrdinalIgnoreCase) || string.Equals(command.Arguments[1], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteExternalListAsync(context, cancellationToken);
            }

            if (string.Equals(command.Arguments[1], "run", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 4)
            {
                return ExecuteExternalRunAsync(command.Arguments[2], string.Join(' ', command.Arguments.Skip(3)), ExternalAgentMode.WorkspaceWrite, context, cancellationToken);
            }

            return RenderAsync(new CommandResult(false, 1, context.OutputFormat, "Usage: /agents external [list|status|run <adapterId> <prompt>]", null), context, cancellationToken);
        }

        return ExecuteListAsync(context, cancellationToken);
    }

    private Command BuildExternalListCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("list", "Lists external agent adapters.");
        command.SetAction((parseResult, cancellationToken) => ExecuteExternalListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildExternalStatusCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("status", "Shows external agent adapter health.");
        command.SetAction((parseResult, cancellationToken) => ExecuteExternalListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildExternalRunCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("run", "Runs a prompt through an external agent.");
        var adapter = new Option<string>("--adapter") { Required = true, Description = "Adapter id, for example claude or codex." };
        var prompt = new Option<string>("--prompt") { Required = true, Description = "Prompt text." };
        var mode = new Option<string>("--mode") { DefaultValueFactory = _ => "workspaceWrite", Description = "readOnly or workspaceWrite." };
        command.Options.Add(adapter);
        command.Options.Add(prompt);
        command.Options.Add(mode);
        command.SetAction((parseResult, cancellationToken) => ExecuteExternalRunAsync(
            parseResult.GetValue(adapter)!,
            parseResult.GetValue(prompt)!,
            ParseMode(parseResult.GetValue(mode)),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        return command;
    }

    private async Task<int> ExecuteListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var agents = await agentCatalogService.ListAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        var message = replInteractionState.AgentIdOverride is null
            ? $"{agents.Count} agent(s)."
            : $"{agents.Count} agent(s). Active REPL agent override: {replInteractionState.AgentIdOverride}.";
        var result = new CommandResult(
            true,
            0,
            context.OutputFormat,
            message,
            JsonSerializer.Serialize(agents.ToList(), ProtocolJsonContext.Default.ListAgentCatalogEntry));
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteUseAsync(string agentId, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var agents = await agentCatalogService.ListAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        if (!agents.Any(agent => string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase)))
        {
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(false, 1, context.OutputFormat, $"Unknown agent '{agentId}'.", null),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        replInteractionState.AgentIdOverride = agentId;
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"Active REPL agent set to {agentId}.", null),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteExternalListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var report = await externalAgentService.ListAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        var available = report.Agents.Count(agent => agent.Health == ExternalAgentHealth.Available);
        var result = new CommandResult(
            true,
            0,
            context.OutputFormat,
            $"External agents {(report.Enabled ? "enabled" : "disabled")}. {available}/{report.Agents.Count} adapter(s) available.",
            JsonSerializer.Serialize(report, ProtocolJsonContext.Default.ExternalAgentCatalogReport));
        return await RenderAsync(result, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteExternalRunAsync(
        string adapterId,
        string prompt,
        ExternalAgentMode mode,
        CommandExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = await externalAgentService
            .RunAsync(
                new ExternalAgentRunRequest(
                    adapterId,
                    context.WorkingDirectory,
                    prompt,
                    mode,
                    context.SessionId,
                    PermissionMode: context.PermissionMode,
                    PrimaryMode: context.PrimaryMode,
                    IsInteractive: context.OutputFormat == OutputFormat.Text),
                cancellationToken)
            .ConfigureAwait(false);
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
            || string.Equals(value, "write", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalAgentMode.WorkspaceWrite;
        }

        throw new ArgumentException($"Unsupported external agent mode '{value}'. Expected readOnly or workspaceWrite.", nameof(value));
    }
}
