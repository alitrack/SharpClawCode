using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Bootstraps the workspace-local SharpClaw configuration footprint.
/// </summary>
public sealed class InitCommandHandler(
    IWorkspaceBootstrapService workspaceBootstrapService,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "init";

    /// <inheritdoc />
    public string Description => "Creates .sharpclaw/config.jsonc and optional commands/skills directories.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        var forceOption = new Option<bool>("--force") { Description = "Overwrite the workspace config file if it already exists." };
        var commandsOption = new Option<bool>("--commands") { Description = "Create .sharpclaw/commands." };
        var skillsOption = new Option<bool>("--skills") { Description = "Create .sharpclaw/skills." };
        command.Options.Add(forceOption);
        command.Options.Add(commandsOption);
        command.Options.Add(skillsOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            globalOptions.Resolve(parseResult),
            parseResult.GetValue(forceOption),
            parseResult.GetValue(commandsOption),
            parseResult.GetValue(skillsOption),
            cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var force = command.Arguments.Any(static item => string.Equals(item, "force", StringComparison.OrdinalIgnoreCase));
        var includeAll = command.Arguments.Any(static item => string.Equals(item, "all", StringComparison.OrdinalIgnoreCase));
        var includeCommands = includeAll || command.Arguments.Any(static item => string.Equals(item, "commands", StringComparison.OrdinalIgnoreCase));
        var includeSkills = includeAll || command.Arguments.Any(static item => string.Equals(item, "skills", StringComparison.OrdinalIgnoreCase));
        return ExecuteAsync(context, force, includeCommands, includeSkills, cancellationToken);
    }

    private async Task<int> ExecuteAsync(
        CommandExecutionContext context,
        bool force,
        bool includeCommandsDirectory,
        bool includeSkillsDirectory,
        CancellationToken cancellationToken)
    {
        var result = await workspaceBootstrapService
            .InitializeAsync(context.WorkingDirectory, force, includeCommandsDirectory, includeSkillsDirectory, cancellationToken)
            .ConfigureAwait(false);
        var createdDirectories = result.CreatedDirectories.Length == 0
            ? "none"
            : string.Join(", ", result.CreatedDirectories);
        var message = $"Initialized SharpClaw workspace config at {result.ConfigPath}. Created directories: {createdDirectories}.";
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, message, JsonSerializer.Serialize(result)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
