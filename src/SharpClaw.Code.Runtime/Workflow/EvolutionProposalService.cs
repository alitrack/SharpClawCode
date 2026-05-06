using System.Text;
using System.Text.Json;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Memory.Abstractions;
using SharpClaw.Code.Permissions.Abstractions;
using SharpClaw.Code.Permissions.Models;
using SharpClaw.Code.Protocol.Abstractions;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Events;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Runtime.Abstractions;
using SharpClaw.Code.Sessions.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <inheritdoc />
public sealed class EvolutionProposalService(
    IEvolutionProposalStore proposalStore,
    ISessionStore sessionStore,
    IEventStore eventStore,
    IWorkspaceInsightsService workspaceInsightsService,
    IProjectMemoryService projectMemoryService,
    ISessionCoordinator sessionCoordinator,
    ISessionPreferenceService sessionPreferenceService,
    ISpecWorkflowService specWorkflowService,
    IPermissionPolicyEngine permissionPolicyEngine,
    IFileSystem fileSystem,
    IPathService pathService,
    ISystemClock systemClock) : IEvolutionProposalService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<EvolutionProposal>> ListAsync(string workspaceRoot, CancellationToken cancellationToken)
        => proposalStore.ListAsync(pathService.GetFullPath(workspaceRoot), cancellationToken);

    /// <inheritdoc />
    public Task<EvolutionProposal?> GetAsync(string workspaceRoot, string proposalId, CancellationToken cancellationToken)
        => proposalStore.GetByIdAsync(pathService.GetFullPath(workspaceRoot), proposalId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvolutionProposal>> AnalyzeAsync(
        string workspaceRoot,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var existing = (await proposalStore.ListAsync(normalizedWorkspace, cancellationToken).ConfigureAwait(false))
            .ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var currentSessionId = sessionId
            ?? await sessionCoordinator.GetAttachedSessionIdAsync(normalizedWorkspace, cancellationToken).ConfigureAwait(false);
        var stats = await workspaceInsightsService.BuildStatsReportAsync(normalizedWorkspace, currentSessionId, cancellationToken).ConfigureAwait(false);
        var memoryContext = await projectMemoryService.BuildContextAsync(normalizedWorkspace, cancellationToken).ConfigureAwait(false);
        var sessions = await sessionStore.ListAllAsync(normalizedWorkspace, cancellationToken).ConfigureAwait(false);

        var failedTurns = 0;
        var permissionDenials = 0;
        var toolFailures = 0;
        foreach (var session in sessions)
        {
            var events = await eventStore.ReadAllAsync(normalizedWorkspace, session.Id, cancellationToken).ConfigureAwait(false);
            failedTurns += events.OfType<TurnCompletedEvent>().Count(static item => !item.Succeeded);
            permissionDenials += events.OfType<PermissionResolvedEvent>().Count(static item => !item.Decision.IsAllowed);
            toolFailures += events.OfType<ToolCompletedEvent>().Count(static item => !item.Result.Succeeded);
        }

        var candidates = new List<EvolutionProposal>();
        if (memoryContext.Memory is null)
        {
            candidates.Add(BuildProposal(
                "evolution-knowledge-refresh",
                normalizedWorkspace,
                EvolutionProposalCategory.KnowledgeRefresh,
                "Create project memory",
                "The workspace has no durable SharpClaw memory document, so repeated expectations are likely to be relearned every session.",
                [
                    "No .sharpclaw/SHARPCLAW.md document was found.",
                    $"{stats.SessionCount} persisted session(s) already exist for this workspace."
                ],
                [
                    "Create .sharpclaw/SHARPCLAW.md with the current delivery rules, architecture boundaries, and operator preferences."
                ]));
        }

        if (permissionDenials >= 2)
        {
            candidates.Add(BuildProposal(
                "evolution-approval-defaults",
                normalizedWorkspace,
                EvolutionProposalCategory.ApprovalDefaults,
                "Tighten approval defaults to a durable session preference",
                "Permission denials are recurring often enough that the workspace should pin an explicit session default instead of relying on ad hoc retries.",
                [
                    $"{permissionDenials} permission denial event(s) were recorded across persisted sessions.",
                    $"{stats.ProviderRequestCount} provider request(s) and {stats.ToolExecutionCount} tool execution(s) were observed."
                ],
                [
                    "Persist workspaceWrite as the preferred session permission mode for the active session."
                ]));
        }

        if (failedTurns >= 2 || toolFailures >= 3)
        {
            candidates.Add(BuildProposal(
                "evolution-prompt-policy",
                normalizedWorkspace,
                EvolutionProposalCategory.PromptPolicy,
                "Append a sharper delivery policy to project memory",
                "Repeated failed turns or tool failures suggest the agent needs tighter local execution guidance that survives across sessions.",
                [
                    $"{failedTurns} failed turn(s) were recorded.",
                    $"{toolFailures} failed tool execution(s) were recorded."
                ],
                [
                    "Append a short policy section instructing the agent to prefer smaller reversible steps, explicit assumptions, and immediate failure reporting."
                ]));
        }

        if (failedTurns >= 3)
        {
            candidates.Add(BuildProposal(
                "evolution-code-spec",
                normalizedWorkspace,
                EvolutionProposalCategory.CodeSpec,
                "Materialize a recovery spec for unstable workflows",
                "The workspace has enough repeated failures that the next iteration should be driven by a spec artifact instead of another unconstrained execution loop.",
                [
                    $"{failedTurns} failed turn(s) were recorded.",
                    $"{stats.ActiveTodoCount} active todo item(s) remain open."
                ],
                [
                    "Generate a spec artifact set covering failure modes, guardrails, and the next implementation slice."
                ]));
        }

        foreach (var candidate in candidates)
        {
            if (existing.TryGetValue(candidate.Id, out var prior)
                && prior.Status is EvolutionProposalStatus.Applied or EvolutionProposalStatus.Rejected)
            {
                continue;
            }

            await proposalStore.SaveAsync(normalizedWorkspace, candidate, cancellationToken).ConfigureAwait(false);
            existing[candidate.Id] = candidate;
        }

        return existing.Values
            .OrderByDescending(static item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<EvolutionProposal> ApplyAsync(
        string workspaceRoot,
        string proposalId,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var proposal = await proposalStore.GetByIdAsync(normalizedWorkspace, proposalId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Evolution proposal '{proposalId}' was not found.");
        if (proposal.Status != EvolutionProposalStatus.Open)
        {
            throw new InvalidOperationException($"Evolution proposal '{proposalId}' is already {proposal.Status}.");
        }

        await RequireApprovalAsync(normalizedWorkspace, proposal, context, cancellationToken).ConfigureAwait(false);

        var now = systemClock.UtcNow;
        var updated = proposal.Category switch
        {
            EvolutionProposalCategory.ApprovalDefaults => await ApplyApprovalDefaultsAsync(normalizedWorkspace, proposal, context, now, cancellationToken).ConfigureAwait(false),
            EvolutionProposalCategory.PromptPolicy => await ApplyPromptPolicyAsync(normalizedWorkspace, proposal, context, now, cancellationToken).ConfigureAwait(false),
            EvolutionProposalCategory.KnowledgeRefresh => await ApplyKnowledgeRefreshAsync(normalizedWorkspace, proposal, context, now, cancellationToken).ConfigureAwait(false),
            EvolutionProposalCategory.CodeSpec => await ApplyCodeSpecAsync(normalizedWorkspace, proposal, context, now, cancellationToken).ConfigureAwait(false),
            EvolutionProposalCategory.ModelRouting => throw new InvalidOperationException("Model-routing proposals are not generated automatically yet. Set an explicit model with /model use."),
            EvolutionProposalCategory.SkillSuggestion => throw new InvalidOperationException("Skill suggestion proposals are advisory only in this build."),
            EvolutionProposalCategory.PluginSuggestion => throw new InvalidOperationException("Plugin suggestion proposals are advisory only in this build."),
            _ => throw new InvalidOperationException($"Unsupported evolution proposal category '{proposal.Category}'."),
        };

        await proposalStore.SaveAsync(normalizedWorkspace, updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc />
    public async Task<EvolutionProposal> RejectAsync(
        string workspaceRoot,
        string proposalId,
        string? rejectedBy,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var proposal = await proposalStore.GetByIdAsync(normalizedWorkspace, proposalId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Evolution proposal '{proposalId}' was not found.");
        var updated = proposal with
        {
            Status = EvolutionProposalStatus.Rejected,
            UpdatedAtUtc = systemClock.UtcNow,
            AppliedBy = string.IsNullOrWhiteSpace(rejectedBy) ? proposal.AppliedBy : rejectedBy,
        };
        await proposalStore.SaveAsync(normalizedWorkspace, updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task RequireApprovalAsync(
        string workspaceRoot,
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var scope = proposal.Category is EvolutionProposalCategory.PromptPolicy
            or EvolutionProposalCategory.KnowledgeRefresh
            or EvolutionProposalCategory.CodeSpec
            ? ApprovalScope.FileSystemWrite
            : ApprovalScope.SessionOperation;
        var sessionId = context.SessionId
            ?? await sessionCoordinator.GetAttachedSessionIdAsync(workspaceRoot, cancellationToken).ConfigureAwait(false)
            ?? "evolution-workspace";
        var request = new ToolExecutionRequest(
            Id: $"evolution-{proposal.Id}",
            SessionId: sessionId,
            TurnId: "evolution-apply",
            ToolName: $"evolution.apply.{proposal.Category}",
            ArgumentsJson: JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["proposalId"] = proposal.Id,
                ["category"] = proposal.Category.ToString()
            }),
            ApprovalScope: scope,
            WorkingDirectory: workspaceRoot,
            RequiresApproval: true,
            IsDestructive: true);
        var evaluationContext = new PermissionEvaluationContext(
            SessionId: sessionId,
            WorkspaceRoot: workspaceRoot,
            WorkingDirectory: workspaceRoot,
            PermissionMode: context.PermissionMode,
            AllowedTools: null,
            AllowDangerousBypass: false,
            IsInteractive: context.IsInteractive,
            SourceKind: PermissionRequestSourceKind.Runtime,
            SourceName: "evolution",
            TrustedPluginNames: null,
            TrustedMcpServerNames: null,
            ToolOriginatingPluginId: null,
            ToolOriginatingPluginTrust: null,
            PrimaryMode: context.PrimaryMode ?? PrimaryMode.Build,
            TenantId: context.HostContext?.TenantId,
            ApprovalSettings: context.ApprovalSettings);
        var decision = await permissionPolicyEngine.EvaluateAsync(request, evaluationContext, cancellationToken).ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            throw new InvalidOperationException(decision.Reason ?? $"Approval was denied for proposal '{proposal.Id}'.");
        }
    }

    private async Task<EvolutionProposal> ApplyApprovalDefaultsAsync(
        string workspaceRoot,
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await sessionPreferenceService
            .SetPreferredPermissionModeAsync(workspaceRoot, context.SessionId, PermissionMode.WorkspaceWrite, cancellationToken)
            .ConfigureAwait(false);
        return MarkApplied(proposal, context, now, "Persisted workspaceWrite as the preferred session permission mode.");
    }

    private async Task<EvolutionProposal> ApplyPromptPolicyAsync(
        string workspaceRoot,
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await AppendProjectMemorySectionAsync(
            workspaceRoot,
            "Execution policy",
            [
                "Prefer smaller reversible implementation steps.",
                "State assumptions explicitly before relying on them.",
                "Report suspected bugs immediately instead of silently correcting them."
            ],
            cancellationToken).ConfigureAwait(false);
        return MarkApplied(proposal, context, now, "Appended an execution-policy section to .sharpclaw/SHARPCLAW.md.");
    }

    private async Task<EvolutionProposal> ApplyKnowledgeRefreshAsync(
        string workspaceRoot,
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await AppendProjectMemorySectionAsync(
            workspaceRoot,
            "Project memory",
            [
                "Document architecture boundaries, delivery expectations, and common failure modes here.",
                "Keep this file current when operator preferences or runtime policies change."
            ],
            cancellationToken).ConfigureAwait(false);
        return MarkApplied(proposal, context, now, "Created or refreshed .sharpclaw/SHARPCLAW.md.");
    }

    private async Task<EvolutionProposal> ApplyCodeSpecAsync(
        string workspaceRoot,
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            requirements = new
            {
                title = proposal.Title,
                summary = proposal.Summary,
                requirements = proposal.Evidence.Select((evidence, index) => new
                {
                    id = $"REQ-{index + 1:000}",
                    statement = $"When the current workspace signals recur, the system shall address {evidence.ToLowerInvariant()}.",
                    rationale = "Generated from workspace evolution analysis."
                }).ToArray()
            },
            design = new
            {
                title = $"{proposal.Title} Design",
                summary = proposal.Summary,
                architecture = proposal.RecommendedActions,
                dataFlow = proposal.Evidence,
                interfaces = new[] { "Runtime command service", "Session metadata", "Project memory" },
                failureModes = new[] { "Repeated failed turns should become explicit recovery work." },
                testing = new[] { "Review the generated spec before the next implementation pass." }
            },
            tasks = new
            {
                title = $"{proposal.Title} Tasks",
                tasks = proposal.RecommendedActions.Select((action, index) => new
                {
                    id = $"TASK-{index + 1:000}",
                    description = action,
                    doneCriteria = "The change is reflected in the workspace and no longer repeats in evolution analysis."
                }).ToArray()
            }
        });
        var artifacts = await specWorkflowService
            .MaterializeAsync(workspaceRoot, proposal.Title, payload, cancellationToken)
            .ConfigureAwait(false);
        return MarkApplied(proposal, context, now, $"Materialized recovery spec artifacts at {artifacts.RootPath}.");
    }

    private async Task AppendProjectMemorySectionAsync(
        string workspaceRoot,
        string heading,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        var memoryRoot = pathService.Combine(workspaceRoot, ".sharpclaw");
        var memoryPath = pathService.Combine(memoryRoot, "SHARPCLAW.md");
        fileSystem.CreateDirectory(memoryRoot);

        var existing = await fileSystem.ReadAllTextIfExistsAsync(memoryPath, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder(string.IsNullOrWhiteSpace(existing) ? string.Empty : existing!.TrimEnd() + Environment.NewLine + Environment.NewLine);
        if (!string.IsNullOrWhiteSpace(existing) && existing.Contains($"## {heading}", StringComparison.Ordinal))
        {
            return;
        }

        builder.Append("## ").AppendLine(heading).AppendLine();
        foreach (var line in lines)
        {
            builder.Append("- ").AppendLine(line);
        }

        await fileSystem.WriteAllTextAsync(memoryPath, builder.ToString().TrimEnd() + Environment.NewLine, cancellationToken).ConfigureAwait(false);
    }

    private EvolutionProposal BuildProposal(
        string id,
        string workspaceRoot,
        EvolutionProposalCategory category,
        string title,
        string summary,
        string[] evidence,
        string[] actions)
        => new(
            Id: id,
            WorkspaceRoot: workspaceRoot,
            Category: category,
            Status: EvolutionProposalStatus.Open,
            Title: title,
            Summary: summary,
            Evidence: evidence,
            RecommendedActions: actions,
            CreatedAtUtc: systemClock.UtcNow,
            UpdatedAtUtc: systemClock.UtcNow);

    private static EvolutionProposal MarkApplied(
        EvolutionProposal proposal,
        RuntimeCommandContext context,
        DateTimeOffset now,
        string actionNote)
        => proposal with
        {
            Status = EvolutionProposalStatus.Applied,
            UpdatedAtUtc = now,
            AppliedBy = context.AgentId ?? "cli",
            RecommendedActions = proposal.RecommendedActions.Concat([actionNote]).ToArray(),
        };
}
