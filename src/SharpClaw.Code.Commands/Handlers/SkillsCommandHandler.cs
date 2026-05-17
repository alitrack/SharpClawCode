using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Events;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;
using SharpClaw.Code.Skills.Abstractions;
using SharpClaw.Code.Skills.Models;
using SharpClaw.Code.Telemetry;
using SharpClaw.Code.Telemetry.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Manages workspace-local skills under <c>.sharpclaw/skills</c>.
/// </summary>
public sealed class SkillsCommandHandler(
    ISkillRegistry skillRegistry,
    ISkillPackRegistry skillPackRegistry,
    IRuntimeCommandService runtimeCommandService,
    IRuntimeEventPublisher eventPublisher,
    ISystemClock systemClock,
    IFileSystem fileSystem,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "skills";

    /// <inheritdoc />
    public string Description => "Lists, installs, inspects, and removes local skills.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        command.Subcommands.Add(BuildListCommand(globalOptions));
        command.Subcommands.Add(BuildShowCommand(globalOptions));
        command.Subcommands.Add(BuildInstallCommand(globalOptions));
        command.Subcommands.Add(BuildEnableCommand(globalOptions));
        command.Subcommands.Add(BuildDisableCommand(globalOptions));
        command.Subcommands.Add(BuildRunCommand(globalOptions));
        command.Subcommands.Add(BuildUninstallCommand(globalOptions));
        command.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
        => command.Arguments.Length switch
        {
            0 => ExecuteListAsync(context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "list", StringComparison.OrdinalIgnoreCase)
                => ExecuteListAsync(context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "show", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteShowAsync(command.Arguments[1], context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "install", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteInstallPackAsync(command.Arguments[1], context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "enable", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteEnableAsync(command.Arguments[1], context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "disable", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteDisableAsync(command.Arguments[1], context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "run", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteRunAsync(command.Arguments[1], string.Join(' ', command.Arguments.Skip(2)), null, context, cancellationToken),
            _ when string.Equals(command.Arguments[0], "uninstall", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2
                => ExecuteUninstallAsync(command.Arguments[1], context, cancellationToken),
            _ => ExecuteListAsync(context, cancellationToken)
        };

    private Command BuildListCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("list", "Lists installed skills.");
        command.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildShowCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("show", "Shows a skill manifest and prompt.");
        var idOption = new Option<string>("--id") { Required = true, Description = "Skill id or name." };
        command.Options.Add(idOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(parseResult.GetValue(idOption)!, globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildInstallCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("install", "Installs a skill from a JSON manifest.");
        var pathArgument = new Argument<string?>("path")
        {
            Description = "Path to a skillpack.json file or skill-pack directory.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var manifestOption = new Option<string?>("--manifest") { Description = "Path to a serialized legacy SkillInstallRequest JSON document." };
        var pathOption = new Option<string?>("--path") { Description = "Path to a skillpack.json file or skill-pack directory." };
        command.Arguments.Add(pathArgument);
        command.Options.Add(manifestOption);
        command.Options.Add(pathOption);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var legacyManifest = parseResult.GetValue(manifestOption);
            var path = parseResult.GetValue(pathOption) ?? parseResult.GetValue(pathArgument);
            return string.IsNullOrWhiteSpace(path)
                ? ExecuteInstallAsync(legacyManifest ?? throw new InvalidOperationException("--manifest or --path is required."), globalOptions.Resolve(parseResult), cancellationToken)
                : ExecuteInstallPackAsync(path, globalOptions.Resolve(parseResult), cancellationToken);
        });
        return command;
    }

    private Command BuildEnableCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("enable", "Enables a workspace skill pack.");
        var idOption = new Option<string>("--id") { Required = true, Description = "Skill pack id." };
        command.Options.Add(idOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteEnableAsync(parseResult.GetValue(idOption)!, globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildDisableCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("disable", "Disables a workspace skill pack.");
        var idOption = new Option<string>("--id") { Required = true, Description = "Skill pack id." };
        command.Options.Add(idOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteDisableAsync(parseResult.GetValue(idOption)!, globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildRunCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("run", "Runs a skill pack through the normal runtime prompt pipeline.");
        var idArgument = new Argument<string?>("id")
        {
            Description = "Skill pack id.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var idOption = new Option<string?>("--id") { Description = "Skill pack id." };
        var argsOption = new Option<string?>("--args") { Description = "Arguments inserted into the skill prompt." };
        var commandOption = new Option<string?>("--command") { Description = "Named skill command to run." };
        command.Arguments.Add(idArgument);
        command.Options.Add(idOption);
        command.Options.Add(argsOption);
        command.Options.Add(commandOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteRunAsync(parseResult.GetValue(idOption) ?? parseResult.GetValue(idArgument) ?? throw new InvalidOperationException("Skill pack id is required."), parseResult.GetValue(argsOption), parseResult.GetValue(commandOption), globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private Command BuildUninstallCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("uninstall", "Removes an installed skill.");
        var idOption = new Option<string>("--id") { Required = true, Description = "Skill id." };
        command.Options.Add(idOption);
        command.SetAction((parseResult, cancellationToken) => ExecuteUninstallAsync(parseResult.GetValue(idOption)!, globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    private async Task<int> ExecuteListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var skills = await skillPackRegistry.ListAsync(context.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        var result = new CommandResult(
            true,
            0,
            context.OutputFormat,
            skills.Count == 0 ? "No skills installed." : $"{skills.Count} skill(s).",
            JsonSerializer.Serialize(skills.ToList(), ProtocolJsonContext.Default.ListSkillPack));
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteShowAsync(string id, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var pack = await skillPackRegistry.ResolveAsync(context.WorkingDirectory, id, cancellationToken).ConfigureAwait(false);
        if (pack is not null)
        {
            var expanded = await skillPackRegistry
                .BuildPromptAsync(context.WorkingDirectory, new SkillPackRunRequest(pack.Manifest.Id, null), cancellationToken)
                .ConfigureAwait(false);
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(true, 0, context.OutputFormat, $"{pack.Manifest.Id}: {pack.Manifest.Description}", JsonSerializer.Serialize(new SkillPackInspectionRecord(pack, expanded), ProtocolJsonContext.Default.SkillPackInspectionRecord)),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var skill = await skillRegistry.ResolveAsync(context.WorkingDirectory, id, cancellationToken).ConfigureAwait(false);
        if (skill is null)
        {
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(false, 1, context.OutputFormat, $"Skill '{id}' was not found.", null),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var payload = new SkillInspectionRecord(skill.Definition, skill.PromptTemplate, skill.Metadata);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"{skill.Definition.Id}: {skill.Definition.Description}", JsonSerializer.Serialize(payload, ProtocolJsonContext.Default.SkillInspectionRecord)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteInstallAsync(string manifestPath, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var manifestJson = await fileSystem.ReadAllTextIfExistsAsync(manifestPath, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Skill manifest '{manifestPath}' was not found.");
        var request = JsonSerializer.Deserialize<SkillInstallRequest>(manifestJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Skill manifest '{manifestPath}' could not be parsed.");
        var installed = await skillRegistry.InstallAsync(context.WorkingDirectory, request, cancellationToken).ConfigureAwait(false);
        var payload = new SkillInspectionRecord(installed.Definition, installed.PromptTemplate, installed.Metadata);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"Installed skill '{installed.Definition.Id}'.", JsonSerializer.Serialize(payload, ProtocolJsonContext.Default.SkillInspectionRecord)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteInstallPackAsync(string sourcePath, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var installed = await skillPackRegistry
            .InstallAsync(context.WorkingDirectory, new SkillPackInstallRequest(sourcePath), cancellationToken)
            .ConfigureAwait(false);
        await eventPublisher.PublishAsync(
            new SkillPackInstalledEvent($"event_{Guid.NewGuid():N}", context.SessionId ?? "system", null, systemClock.UtcNow, installed.Manifest.Id, installed.Manifest.Version, installed.Source),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"Installed skill pack '{installed.Manifest.Id}'.", JsonSerializer.Serialize(installed, ProtocolJsonContext.Default.SkillPack)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteEnableAsync(string id, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var enabled = await skillPackRegistry.EnableAsync(context.WorkingDirectory, id, cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(enabled, enabled ? 0 : 1, context.OutputFormat, enabled ? $"Enabled skill pack '{id}'." : $"Skill pack '{id}' was not found.", null),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return enabled ? 0 : 1;
    }

    private async Task<int> ExecuteDisableAsync(string id, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var disabled = await skillPackRegistry.DisableAsync(context.WorkingDirectory, id, cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(disabled, disabled ? 0 : 1, context.OutputFormat, disabled ? $"Disabled skill pack '{id}'." : $"Skill pack '{id}' was not found.", null),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return disabled ? 0 : 1;
    }

    private async Task<int> ExecuteRunAsync(string id, string? arguments, string? commandName, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var prompt = await skillPackRegistry
            .BuildPromptAsync(context.WorkingDirectory, new SkillPackRunRequest(id, arguments, context.PrimaryMode, commandName), cancellationToken)
            .ConfigureAwait(false);
        var result = await runtimeCommandService
            .ExecutePromptAsync(prompt, context.ToRuntimeCommandContext(), cancellationToken)
            .ConfigureAwait(false);
        await eventPublisher.PublishAsync(
            new SkillInvokedEvent($"event_{Guid.NewGuid():N}", result.Session.Id, result.Turn.Id, systemClock.UtcNow, id, commandName),
            new RuntimeEventPublishOptions(context.WorkingDirectory, result.Session.Id, PersistToSessionStore: true),
            cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderTurnExecutionResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteUninstallAsync(string id, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var removed = await skillRegistry.UninstallAsync(context.WorkingDirectory, id, cancellationToken).ConfigureAwait(false);
        var exitCode = removed ? 0 : 1;
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(
                removed,
                exitCode,
                context.OutputFormat,
                removed ? $"Removed skill '{id}'." : $"Skill '{id}' was not found.",
                JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = id }, ProtocolJsonContext.Default.DictionaryStringString)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return exitCode;
    }
}
