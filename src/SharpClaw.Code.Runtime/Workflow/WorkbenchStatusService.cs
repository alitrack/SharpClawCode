using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Events;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Operational;
using SharpClaw.Code.Runtime.Abstractions;
using SharpClaw.Code.Runtime.Diagnostics;
using SharpClaw.Code.Sessions.Abstractions;
using SharpClaw.Code.Telemetry;
using SharpClaw.Code.Telemetry.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <summary>
/// Assembles static workbench state from session stores, checkpoints, diagnostics, and external adapter health.
/// </summary>
public sealed class WorkbenchStatusService(
    ISessionStore sessionStore,
    IEventStore eventStore,
    ICheckpointStore checkpointStore,
    IExternalAgentService externalAgentService,
    IOperationalDiagnosticsCoordinator diagnosticsCoordinator,
    IRuntimeEventPublisher eventPublisher,
    IPathService pathService,
    ISystemClock systemClock) : IWorkbenchStatusService
{
    /// <inheritdoc />
    public async Task<WorkbenchStatusReport> BuildAsync(RuntimeCommandContext context, CancellationToken cancellationToken)
    {
        var workspace = pathService.GetFullPath(context.WorkingDirectory);
        var session = string.IsNullOrWhiteSpace(context.SessionId)
            ? await sessionStore.GetLatestAsync(workspace, cancellationToken).ConfigureAwait(false)
            : await sessionStore.GetByIdAsync(workspace, context.SessionId, cancellationToken).ConfigureAwait(false);
        var checkpoint = session is null
            ? null
            : await checkpointStore.GetLatestAsync(workspace, session.Id, cancellationToken).ConfigureAwait(false);
        var events = session is null
            ? Array.Empty<RuntimeEvent>()
            : (await eventStore.ReadAllAsync(workspace, session.Id, cancellationToken).ConfigureAwait(false)).TakeLast(8).ToArray();
        var external = await externalAgentService.ListAsync(workspace, cancellationToken).ConfigureAwait(false);
        var status = await diagnosticsCoordinator.BuildStatusReportAsync(
            new OperationalDiagnosticsInput(workspace, context.Model, context.PermissionMode, context.OutputFormat, context.PrimaryMode, context.ApprovalSettings),
            cancellationToken).ConfigureAwait(false);
        var primaryMode = context.PrimaryMode ?? ResolvePrimaryMode(session);
        var report = new WorkbenchStatusReport(
            "1.0",
            systemClock.UtcNow,
            workspace,
            session?.Id,
            ResolveGoal(session),
            primaryMode,
            ResolveMetadata(session, SharpClawWorkflowMetadataKeys.ActiveAgentId),
            status.RuntimeState,
            status.ApprovalSettings,
            0,
            checkpoint,
            events.Select(Summarize).ToArray(),
            external.Agents,
            status.Checks.Where(check => check.Status is not OperationalCheckStatus.Ok and not OperationalCheckStatus.Skipped).ToArray());

        if (session is not null)
        {
            await eventPublisher.PublishAsync(
                new WorkbenchViewedEvent($"event_{Guid.NewGuid():N}", session.Id, null, systemClock.UtcNow, workspace),
                new RuntimeEventPublishOptions(workspace, session.Id, PersistToSessionStore: true),
                cancellationToken).ConfigureAwait(false);
        }

        return report;
    }

    private static string? ResolveGoal(ConversationSession? session)
        => ResolveMetadata(session, SharpClawWorkflowMetadataKeys.WorkItemGoal)
            ?? ResolveMetadata(session, SharpClawWorkflowMetadataKeys.DeepPlanningSummary)
            ?? session?.Title;

    private static string? ResolveMetadata(ConversationSession? session, string key)
        => session?.Metadata is not null && session.Metadata.TryGetValue(key, out var value) ? value : null;

    private static PrimaryMode ResolvePrimaryMode(ConversationSession? session)
        => ResolveMetadata(session, SharpClawWorkflowMetadataKeys.PrimaryMode) is { } stored
            && Enum.TryParse<PrimaryMode>(stored, ignoreCase: true, out var parsed)
                ? parsed
                : PrimaryMode.Build;

    private static string Summarize(RuntimeEvent runtimeEvent)
        => runtimeEvent switch
        {
            TurnStartedEvent turn => $"turnStarted {turn.Turn.SequenceNumber}",
            TurnCompletedEvent turn => $"turnCompleted {turn.Turn.SequenceNumber} ok={turn.Succeeded}",
            ToolCompletedEvent tool => $"toolCompleted {tool.Result.ToolName} ok={tool.Result.Succeeded}",
            ExternalAgentRunStartedEvent external => $"externalAgentStarted {external.AdapterId}",
            ExternalAgentRunCompletedEvent external => $"externalAgentCompleted {external.AdapterId}",
            ExternalAgentRunFailedEvent external => $"externalAgentFailed {external.AdapterId}: {external.FailureKind}",
            SkillInvokedEvent skill => $"skillInvoked {skill.SkillId}",
            WorkItemImportedEvent work => $"workItemImported {work.WorkItem.Provider}:{work.WorkItem.Id}",
            WorkbenchViewedEvent => "workbenchViewed",
            _ => runtimeEvent.GetType().Name,
        };
}
