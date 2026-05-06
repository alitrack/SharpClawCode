using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Shows and persists durable session permission settings, approval defaults, and trusted sources.
/// </summary>
public sealed class PermissionsCommandHandler(
    ISessionPreferenceService sessionPreferenceService,
    ReplInteractionState replInteractionState,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "permissions";

    /// <inheritdoc />
    public string Description => "Shows or persists session permission mode, approvals, and trusted MCP/plugin sources.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);

        var show = new Command("show", "Shows the effective durable permission snapshot.");
        show.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(show);

        var mode = new Command("mode", "Shows or sets the durable session permission mode.");
        var modeSet = new Command("set", "Persists the session permission mode.");
        var modeArgument = new Argument<string>("mode") { Description = "readOnly, workspaceWrite, or dangerFullAccess." };
        modeSet.Arguments.Add(modeArgument);
        modeSet.SetAction((parseResult, cancellationToken) => ExecuteSetModeAsync(
            parseResult.GetValue(modeArgument) ?? throw new InvalidOperationException("mode is required."),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        mode.Subcommands.Add(modeSet);
        mode.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(mode);

        var approvals = new Command("approvals", "Shows or sets durable auto-approval settings.");
        var approvalsSet = new Command("set", "Persists approval scopes and optional budget.");
        var scopesArgument = new Argument<string>("scopes") { Description = "Comma-separated scopes: tool,file,shell,network,session,promptRead,all,none." };
        var budgetOption = new Option<int?>("--budget") { Description = "Optional auto-approval budget." };
        approvalsSet.Arguments.Add(scopesArgument);
        approvalsSet.Options.Add(budgetOption);
        approvalsSet.SetAction((parseResult, cancellationToken) => ExecuteSetApprovalsAsync(
            parseResult.GetValue(scopesArgument) ?? throw new InvalidOperationException("scopes are required."),
            parseResult.GetValue(budgetOption),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        approvals.Subcommands.Add(approvalsSet);

        var approvalsClear = new Command("clear", "Clears durable auto-approval settings.");
        approvalsClear.SetAction((parseResult, cancellationToken) => ExecuteClearApprovalsAsync(globalOptions.Resolve(parseResult), cancellationToken));
        approvals.Subcommands.Add(approvalsClear);

        var approvalsShow = new Command("show", "Shows durable auto-approval settings.");
        approvalsShow.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        approvals.Subcommands.Add(approvalsShow);
        approvals.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(approvals);

        var trust = new Command("trust", "Lists or modifies durable trusted plugin and MCP sources.");
        var trustList = new Command("list", "Lists trusted plugin and MCP sources.");
        trustList.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        trust.Subcommands.Add(trustList);

        var trustGrant = new Command("grant", "Grants durable trust to one plugin or MCP server.");
        var kindArgument = new Argument<string>("kind") { Description = "plugin or mcp." };
        var nameArgument = new Argument<string>("name") { Description = "Plugin id or MCP server name." };
        trustGrant.Arguments.Add(kindArgument);
        trustGrant.Arguments.Add(nameArgument);
        trustGrant.SetAction((parseResult, cancellationToken) => ExecuteTrustAsync(
            parseResult.GetValue(kindArgument) ?? throw new InvalidOperationException("kind is required."),
            parseResult.GetValue(nameArgument) ?? throw new InvalidOperationException("name is required."),
            grant: true,
            globalOptions.Resolve(parseResult),
            cancellationToken));
        trust.Subcommands.Add(trustGrant);

        var trustRevoke = new Command("revoke", "Revokes durable trust from one plugin or MCP server.");
        var revokeKindArgument = new Argument<string>("kind") { Description = "plugin or mcp." };
        var revokeNameArgument = new Argument<string>("name") { Description = "Plugin id or MCP server name." };
        trustRevoke.Arguments.Add(revokeKindArgument);
        trustRevoke.Arguments.Add(revokeNameArgument);
        trustRevoke.SetAction((parseResult, cancellationToken) => ExecuteTrustAsync(
            parseResult.GetValue(revokeKindArgument) ?? throw new InvalidOperationException("kind is required."),
            parseResult.GetValue(revokeNameArgument) ?? throw new InvalidOperationException("name is required."),
            grant: false,
            globalOptions.Resolve(parseResult),
            cancellationToken));
        trust.Subcommands.Add(trustRevoke);
        trust.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(trust);

        command.SetAction((parseResult, cancellationToken) => ExecuteShowAsync(globalOptions.Resolve(parseResult), cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length == 0 || string.Equals(command.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteShowAsync(context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "mode", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Arguments.Length >= 3 && string.Equals(command.Arguments[1], "set", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteSetModeAsync(command.Arguments[2], context, cancellationToken);
            }

            return RenderAsync("Usage: /permissions mode set <readOnly|workspaceWrite|dangerFullAccess>", context, false, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "approvals", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Arguments.Length == 1 || string.Equals(command.Arguments[1], "show", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteShowAsync(context, cancellationToken);
            }

            if (string.Equals(command.Arguments[1], "clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command.Arguments[1], "reset", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteClearApprovalsAsync(context, cancellationToken);
            }

            if (string.Equals(command.Arguments[1], "set", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 3)
            {
                var budget = command.Arguments.Length >= 4 && int.TryParse(command.Arguments[3], out var parsedBudget)
                    ? parsedBudget
                    : (int?)null;
                return ExecuteSetApprovalsAsync(command.Arguments[2], budget, context, cancellationToken);
            }

            return RenderAsync("Usage: /permissions approvals [show|set <scopes> [budget]|clear]", context, false, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "trust", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Arguments.Length == 1 || string.Equals(command.Arguments[1], "list", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteShowAsync(context, cancellationToken);
            }

            if (command.Arguments.Length >= 4
                && (string.Equals(command.Arguments[1], "grant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command.Arguments[1], "revoke", StringComparison.OrdinalIgnoreCase)))
            {
                return ExecuteTrustAsync(
                    command.Arguments[2],
                    command.Arguments[3],
                    string.Equals(command.Arguments[1], "grant", StringComparison.OrdinalIgnoreCase),
                    context,
                    cancellationToken);
            }

            return RenderAsync("Usage: /permissions trust [list|grant <plugin|mcp> <name>|revoke <plugin|mcp> <name>]", context, false, cancellationToken);
        }

        return RenderAsync("Usage: /permissions [show|mode|approvals|trust]", context, false, cancellationToken);
    }

    private async Task<int> ExecuteShowAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var report = await sessionPreferenceService
            .GetPermissionStatusAsync(
                context.WorkingDirectory,
                context.SessionId,
                replInteractionState.PermissionModeOverride ?? context.PermissionMode,
                context.ApprovalSettings,
                context.Model,
                cancellationToken)
            .ConfigureAwait(false);
        var message = $"Permission mode: {report.PermissionMode}. Auto-approvals: {ApprovalSettingsText.RenderSummary(report.ApprovalSettings)}. Trusted sources: {report.TrustedSources.Length}. Attached session: {report.AttachedSessionId ?? "none"}.";
        if (!string.IsNullOrWhiteSpace(report.EffectiveModel))
        {
            message += $" Model: {report.EffectiveModel}.";
        }

        if (replInteractionState.PermissionModeOverride is not null && replInteractionState.PermissionModeOverride != report.PermissionMode)
        {
            message += $" REPL override: {replInteractionState.PermissionModeOverride}.";
        }

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

    private async Task<int> ExecuteSetModeAsync(string permissionModeText, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var parsedMode = ParsePermissionMode(permissionModeText);
        await sessionPreferenceService
            .SetPreferredPermissionModeAsync(context.WorkingDirectory, context.SessionId, parsedMode, cancellationToken)
            .ConfigureAwait(false);
        replInteractionState.PermissionModeOverride = parsedMode;
        return await RenderAsync($"Persisted session permission mode '{parsedMode}'.", context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteSetApprovalsAsync(string scopesText, int? budget, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = ApprovalSettingsText.Parse(scopesText, budget) ?? ApprovalSettings.Empty;
        var persisted = await sessionPreferenceService
            .SetApprovalSettingsAsync(context.WorkingDirectory, context.SessionId, settings, cancellationToken)
            .ConfigureAwait(false);
        replInteractionState.ApprovalSettingsOverride = null;
        return await RenderAsync(
            $"Persisted session auto-approvals: {ApprovalSettingsText.RenderSummary(persisted)}.",
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteClearApprovalsAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var cleared = await sessionPreferenceService
            .ClearApprovalSettingsAsync(context.WorkingDirectory, context.SessionId, cancellationToken)
            .ConfigureAwait(false);
        replInteractionState.ApprovalSettingsOverride = null;
        return await RenderAsync(
            cleared ? "Cleared durable session auto-approval settings." : "No durable session auto-approval settings were found.",
            context,
            cancellationToken,
            cleared).ConfigureAwait(false);
    }

    private async Task<int> ExecuteTrustAsync(
        string kindText,
        string name,
        bool grant,
        CommandExecutionContext context,
        CancellationToken cancellationToken)
    {
        var kind = ParseTrustedSourceKind(kindText);
        var report = grant
            ? await sessionPreferenceService
                .GrantTrustAsync(
                    context.WorkingDirectory,
                    context.SessionId,
                    kind,
                    name,
                    context.PermissionMode,
                    context.ApprovalSettings,
                    context.Model,
                    cancellationToken)
                .ConfigureAwait(false)
            : await sessionPreferenceService
                .RevokeTrustAsync(
                    context.WorkingDirectory,
                    context.SessionId,
                    kind,
                    name,
                    context.PermissionMode,
                    context.ApprovalSettings,
                    context.Model,
                    cancellationToken)
                .ConfigureAwait(false);
        var action = grant ? "Granted" : "Revoked";
        return await RenderAsync(
            new CommandResult(
                true,
                0,
                context.OutputFormat,
                $"{action} durable trust for {kind.ToString().ToLowerInvariant()} '{name.Trim()}'.",
                JsonSerializer.Serialize(report, ProtocolJsonContext.Default.PermissionStatusReport)),
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RenderAsync(string message, CommandExecutionContext context, CancellationToken cancellationToken, bool success = true)
        => await RenderAsync(new CommandResult(success, success ? 0 : 1, context.OutputFormat, message, null), context, cancellationToken).ConfigureAwait(false);

    private async Task<int> RenderAsync(CommandResult result, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        await outputRendererDispatcher.RenderCommandResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private static PermissionMode ParsePermissionMode(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "readonly" or "read-only" => PermissionMode.ReadOnly,
            "workspacewrite" or "workspace-write" or "prompt" or "autoapprovesafe" or "auto-approve-safe" => PermissionMode.WorkspaceWrite,
            "dangerfullaccess" or "danger-full-access" or "fulltrust" or "full-trust" => PermissionMode.DangerFullAccess,
            _ => throw new InvalidOperationException($"Unsupported permission mode '{value}'.")
        };

    private static TrustedSourceKind ParseTrustedSourceKind(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "plugin" => TrustedSourceKind.Plugin,
            "mcp" => TrustedSourceKind.Mcp,
            _ => throw new InvalidOperationException($"Unsupported trusted source kind '{value}'. Use plugin or mcp.")
        };
}
