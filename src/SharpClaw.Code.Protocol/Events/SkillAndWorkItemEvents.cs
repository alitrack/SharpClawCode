using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Protocol.Events;

/// <summary>
/// Raised when a skill pack is installed.
/// </summary>
public sealed record SkillPackInstalledEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string SkillId,
    string Version,
    string Source) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised when a skill pack is invoked.
/// </summary>
public sealed record SkillInvokedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string SkillId,
    string? CommandName) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised when a work item is imported into a session.
/// </summary>
public sealed record WorkItemImportedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    WorkItem WorkItem) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised when a work-item-aware summary is generated.
/// </summary>
public sealed record WorkItemSummaryExportedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string Provider,
    string ExportFormat) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised when a static workbench view is assembled.
/// </summary>
public sealed record WorkbenchViewedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string WorkspaceRoot) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);
