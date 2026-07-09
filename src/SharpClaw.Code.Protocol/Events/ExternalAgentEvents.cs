using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Protocol.Events;

/// <summary>
/// Raised before an external agent process is invoked.
/// </summary>
public sealed record ExternalAgentRunStartedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string AdapterId,
    string WorkspacePath,
    ExternalAgentMode Mode) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised after an external agent process completes successfully.
/// </summary>
public sealed record ExternalAgentRunCompletedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string AdapterId,
    int ExitCode,
    string? ExternalSessionId,
    string OutputPreview) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);

/// <summary>
/// Raised when an external agent process cannot run or exits unsuccessfully.
/// </summary>
public sealed record ExternalAgentRunFailedEvent(
    string EventId,
    string SessionId,
    string? TurnId,
    DateTimeOffset OccurredAtUtc,
    string AdapterId,
    ExternalAgentFailureKind FailureKind,
    string Error) : RuntimeEvent(EventId, SessionId, TurnId, OccurredAtUtc);
