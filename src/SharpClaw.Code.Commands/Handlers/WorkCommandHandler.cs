using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Imports and exports workflow work items.
/// </summary>
public sealed class WorkCommandHandler(
    IWorkItemService workItemService,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "work";

    /// <inheritdoc />
    public string Description => "Imports work items and exports session summaries.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        var import = new Command("import", "Imports a GitHub URL or generic JSON fixture into a session.");
        var url = new Argument<string>("url") { Description = "Work item URL or JSON fixture path." };
        var provider = new Option<string>("--provider") { DefaultValueFactory = _ => "github", Description = "Provider id: github or generic." };
        import.Arguments.Add(url);
        import.Options.Add(provider);
        import.SetAction((parseResult, cancellationToken) => ExecuteImportAsync(parseResult.GetValue(provider)!, parseResult.GetValue(url)!, globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(import);

        var show = new Command("show", "Shows the imported work item for the current session.");
        show.SetAction((parseResult, cancellationToken) => ExecuteExportSummaryAsync("markdown", globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(show);

        var export = new Command("export-summary", "Exports a session summary for the linked work item.");
        var format = new Option<string>("--format") { DefaultValueFactory = _ => "markdown", Description = "markdown or json." };
        export.Options.Add(format);
        export.SetAction((parseResult, cancellationToken) => ExecuteExportSummaryAsync(parseResult.GetValue(format)!, globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(export);
        command.SetAction((parseResult, cancellationToken) => ExecuteExportSummaryAsync("markdown", globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length >= 2 && string.Equals(command.Arguments[0], "import", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteImportAsync("github", command.Arguments[1], context, cancellationToken);
        }

        if (command.Arguments.Length == 0 || string.Equals(command.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteExportSummaryAsync("markdown", context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "export-summary", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteExportSummaryAsync("markdown", context, cancellationToken);
        }

        return RenderAsync(new CommandResult(false, 1, context.OutputFormat, "Usage: /work [import <url>|show|export-summary]", null), context, cancellationToken);
    }

    private async Task<int> ExecuteImportAsync(string provider, string idOrUrl, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var result = await workItemService
            .ImportAsync(new WorkItemImportRequest(provider, idOrUrl, context.WorkingDirectory, SessionId: context.SessionId), cancellationToken)
            .ConfigureAwait(false);
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                $"Imported {result.WorkItem.Provider} work item '{result.WorkItem.Id}' into session {result.SessionId}.",
                JsonSerializer.Serialize(result, ProtocolJsonContext.Default.WorkItemImportResult)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteExportSummaryAsync(string format, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var result = await workItemService
            .ExportSummaryAsync(new WorkItemExportRequest("github", context.SessionId, null, format), context.WorkingDirectory, cancellationToken)
            .ConfigureAwait(false);
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                result.Content,
                JsonSerializer.Serialize(result, ProtocolJsonContext.Default.WorkItemSummaryExport)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RenderAsync(CommandResult result, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
