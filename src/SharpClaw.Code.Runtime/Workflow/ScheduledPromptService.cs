using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Abstractions;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Runtime.Abstractions;
using SharpClaw.Code.Sessions.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <inheritdoc />
public sealed class ScheduledPromptService(
    IScheduledPromptStore scheduledPromptStore,
    IRuntimeCommandService runtimeCommandService,
    IConversationRuntime conversationRuntime,
    ISessionCoordinator sessionCoordinator,
    ISystemClock systemClock,
    IPathService pathService,
    ILogger<ScheduledPromptService> logger) : IScheduledPromptService
{
    private static readonly ConcurrentDictionary<string, byte> InFlightSchedules = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduledPromptDefinition>> ListAsync(string workspaceRoot, CancellationToken cancellationToken)
        => scheduledPromptStore.ListAsync(pathService.GetFullPath(workspaceRoot), cancellationToken);

    /// <inheritdoc />
    public Task<ScheduledPromptDefinition?> GetAsync(string workspaceRoot, string scheduleId, CancellationToken cancellationToken)
        => scheduledPromptStore.GetByIdAsync(pathService.GetFullPath(workspaceRoot), scheduleId, cancellationToken);

    /// <inheritdoc />
    public async Task<ScheduledPromptDefinition> SaveAsync(string workspaceRoot, ScheduledPromptDefinition definition, CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var normalized = Normalize(definition with { WorkspaceRoot = normalizedWorkspace }, systemClock.UtcNow);
        await scheduledPromptStore.SaveAsync(normalizedWorkspace, normalized, cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string workspaceRoot, string scheduleId, CancellationToken cancellationToken)
        => scheduledPromptStore.DeleteAsync(pathService.GetFullPath(workspaceRoot), scheduleId, cancellationToken);

    /// <inheritdoc />
    public async Task<ScheduledPromptDefinition> SetEnabledAsync(
        string workspaceRoot,
        string scheduleId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var schedule = await scheduledPromptStore.GetByIdAsync(normalizedWorkspace, scheduleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Scheduled prompt '{scheduleId}' was not found.");
        var updated = Normalize(schedule with { Enabled = enabled }, systemClock.UtcNow);
        await scheduledPromptStore.SaveAsync(normalizedWorkspace, updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc />
    public async Task<ScheduledPromptRunReport> RunAsync(
        string workspaceRoot,
        string scheduleId,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var schedule = await scheduledPromptStore.GetByIdAsync(normalizedWorkspace, scheduleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Scheduled prompt '{scheduleId}' was not found.");
        return await RunScheduleAsync(normalizedWorkspace, schedule, context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduledPromptRunReport>> RunDueAsync(
        string workspaceRoot,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspace = pathService.GetFullPath(workspaceRoot);
        var schedules = await scheduledPromptStore.ListAsync(normalizedWorkspace, cancellationToken).ConfigureAwait(false);
        var now = systemClock.UtcNow;
        var due = schedules
            .Where(schedule => schedule.Enabled && schedule.NextRunUtc is { } nextRun && nextRun <= now)
            .OrderBy(static schedule => schedule.NextRunUtc)
            .ToArray();

        var reports = new List<ScheduledPromptRunReport>(due.Length);
        foreach (var schedule in due)
        {
            reports.Add(await RunScheduleAsync(normalizedWorkspace, schedule, context, cancellationToken).ConfigureAwait(false));
        }

        return reports;
    }

    private async Task<ScheduledPromptRunReport> RunScheduleAsync(
        string workspaceRoot,
        ScheduledPromptDefinition schedule,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var inflightKey = $"{workspaceRoot}::{schedule.Id}";
        if (!InFlightSchedules.TryAdd(inflightKey, 0))
        {
            return new ScheduledPromptRunReport(
                schedule.Id,
                schedule.Name,
                false,
                "The scheduled prompt is already running in this process.",
                systemClock.UtcNow,
                systemClock.UtcNow);
        }

        var startedAtUtc = systemClock.UtcNow;
        try
        {
            var sessionId = await ResolveSessionIdAsync(workspaceRoot, schedule, cancellationToken).ConfigureAwait(false);
            var runContext = new RuntimeCommandContext(
                WorkingDirectory: workspaceRoot,
                Model: schedule.ModelOverride ?? context.Model,
                PermissionMode: schedule.PermissionMode,
                OutputFormat: OutputFormat.Text,
                PrimaryMode: schedule.PrimaryMode,
                SessionId: sessionId,
                AgentId: context.AgentId,
                IsInteractive: false,
                HostContext: context.HostContext,
                ApprovalSettings: schedule.ApprovalSettings);

            var result = await runtimeCommandService
                .ExecutePromptAsync(schedule.Prompt, runContext, cancellationToken)
                .ConfigureAwait(false);
            var completedAtUtc = systemClock.UtcNow;
            var message = string.IsNullOrWhiteSpace(result.FinalOutput)
                ? $"Completed scheduled prompt '{schedule.Name}'."
                : result.FinalOutput!;

            var updated = Normalize(
                schedule with
                {
                    LastRunUtc = completedAtUtc,
                    LastOutcome = new ScheduledPromptLastOutcome(true, Truncate(message), completedAtUtc, result.Session.Id),
                },
                completedAtUtc);
            await scheduledPromptStore.SaveAsync(workspaceRoot, updated, cancellationToken).ConfigureAwait(false);

            return new ScheduledPromptRunReport(
                schedule.Id,
                schedule.Name,
                true,
                Truncate(message),
                startedAtUtc,
                completedAtUtc,
                result.Session.Id);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Scheduled prompt {ScheduleId} failed.", schedule.Id);
            var completedAtUtc = systemClock.UtcNow;
            var updated = Normalize(
                schedule with
                {
                    LastRunUtc = completedAtUtc,
                    LastOutcome = new ScheduledPromptLastOutcome(false, Truncate(exception.Message), completedAtUtc),
                },
                completedAtUtc);
            await scheduledPromptStore.SaveAsync(workspaceRoot, updated, cancellationToken).ConfigureAwait(false);

            return new ScheduledPromptRunReport(
                schedule.Id,
                schedule.Name,
                false,
                Truncate(exception.Message),
                startedAtUtc,
                completedAtUtc);
        }
        finally
        {
            InFlightSchedules.TryRemove(inflightKey, out _);
        }
    }

    private async Task<string?> ResolveSessionIdAsync(string workspaceRoot, ScheduledPromptDefinition schedule, CancellationToken cancellationToken)
    {
        return schedule.SessionTarget.Kind switch
        {
            ScheduledPromptSessionTargetKind.New => (await conversationRuntime
                .CreateSessionAsync(workspaceRoot, schedule.PermissionMode, OutputFormat.Text, cancellationToken)
                .ConfigureAwait(false)).Id,
            ScheduledPromptSessionTargetKind.Attached => await sessionCoordinator
                .GetAttachedSessionIdAsync(workspaceRoot, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The schedule targets the attached session, but no session is attached for this workspace."),
            ScheduledPromptSessionTargetKind.Explicit => string.IsNullOrWhiteSpace(schedule.SessionTarget.SessionId)
                ? throw new InvalidOperationException("The schedule targets an explicit session, but no session id was configured.")
                : schedule.SessionTarget.SessionId,
            _ => throw new InvalidOperationException($"Unsupported schedule session target '{schedule.SessionTarget.Kind}'."),
        };
    }

    private static ScheduledPromptDefinition Normalize(ScheduledPromptDefinition definition, DateTimeOffset now)
    {
        var nextRunUtc = definition.Enabled
            ? ScheduleCronExpression.GetNextOccurrence(definition.Cron, now)
            : (DateTimeOffset?)null;

        return definition with
        {
            Name = definition.Name.Trim(),
            Prompt = definition.Prompt.Trim(),
            Cron = definition.Cron.Trim(),
            NextRunUtc = nextRunUtc,
        };
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No output.";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240];
    }
}
