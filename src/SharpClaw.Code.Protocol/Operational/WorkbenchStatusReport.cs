using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Protocol.Operational;

/// <summary>
/// Static workbench/status payload focused on runtime state rather than terminal decoration.
/// </summary>
public sealed record WorkbenchStatusReport(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string WorkspaceRoot,
    string? CurrentSessionId,
    string? CurrentGoal,
    PrimaryMode PrimaryMode,
    string? ActiveAgentId,
    string RuntimeState,
    ApprovalSettings? ApprovalSettings,
    RuntimeCheckpoint? LatestCheckpoint,
    IReadOnlyList<string> RecentActivity,
    IReadOnlyList<ExternalAgentStatus> ExternalAgents,
    IReadOnlyList<OperationalCheckItem> Warnings);
