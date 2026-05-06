using System.Text.Json.Serialization;
using SharpClaw.Code.Protocol.Enums;

namespace SharpClaw.Code.Protocol.Models;

/// <summary>
/// Declares a durable trusted-source category.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrustedSourceKind>))]
public enum TrustedSourceKind
{
    /// <summary>Plugin id.</summary>
    [JsonStringEnumMemberName("plugin")]
    Plugin,

    /// <summary>MCP server name.</summary>
    [JsonStringEnumMemberName("mcp")]
    Mcp,
}

/// <summary>
/// One trusted plugin or MCP server persisted for a session.
/// </summary>
public sealed record TrustedSourceEntry(
    TrustedSourceKind Kind,
    string Name,
    DateTimeOffset GrantedAtUtc);

/// <summary>
/// Summarizes the effective permission posture for the active workspace/session.
/// </summary>
public sealed record PermissionStatusReport(
    PermissionMode PermissionMode,
    ApprovalSettings? ApprovalSettings,
    TrustedSourceEntry[] TrustedSources,
    string? AttachedSessionId,
    string? EffectiveModel);

/// <summary>
/// Persists a preferred model selection for a durable session.
/// </summary>
public sealed record SessionModelPreference(
    string? Model,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Declares how a scheduled prompt chooses its target session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScheduledPromptSessionTargetKind>))]
public enum ScheduledPromptSessionTargetKind
{
    /// <summary>Create a fresh session for each run.</summary>
    [JsonStringEnumMemberName("new")]
    New,

    /// <summary>Reuse the current attached session for the workspace.</summary>
    [JsonStringEnumMemberName("attached")]
    Attached,

    /// <summary>Reuse one explicit session id.</summary>
    [JsonStringEnumMemberName("explicit")]
    Explicit,
}

/// <summary>
/// Identifies the session target used by a scheduled prompt.
/// </summary>
public sealed record ScheduledPromptSessionTarget(
    ScheduledPromptSessionTargetKind Kind,
    string? SessionId = null);

/// <summary>
/// Summarizes the last known outcome for one scheduled prompt run.
/// </summary>
public sealed record ScheduledPromptLastOutcome(
    bool Succeeded,
    string Message,
    DateTimeOffset OccurredAtUtc,
    string? SessionId = null);

/// <summary>
/// Durable workspace-local scheduled prompt definition.
/// </summary>
public sealed record ScheduledPromptDefinition(
    string Id,
    string WorkspaceRoot,
    string Name,
    string Prompt,
    string Cron,
    PrimaryMode PrimaryMode,
    string? ModelOverride,
    PermissionMode PermissionMode,
    ApprovalSettings? ApprovalSettings,
    ScheduledPromptSessionTarget SessionTarget,
    bool Enabled,
    DateTimeOffset? LastRunUtc,
    DateTimeOffset? NextRunUtc,
    ScheduledPromptLastOutcome? LastOutcome);

/// <summary>
/// Summarizes one schedule execution attempt.
/// </summary>
public sealed record ScheduledPromptRunReport(
    string ScheduleId,
    string Name,
    bool Succeeded,
    string Message,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? SessionId = null);

/// <summary>
/// Request contract for citation-oriented research mode.
/// </summary>
public sealed record ResearchRequest(
    string Prompt,
    int MaxSources = 8,
    bool UseSubAgents = true);

/// <summary>
/// One cited research source.
/// </summary>
public sealed record ResearchSource(
    string Title,
    string Url,
    string? Snippet,
    string SourceKind,
    string? ConfidenceNote = null);

/// <summary>
/// Structured research report shape used by commands and tests.
/// </summary>
public sealed record ResearchReport(
    string Summary,
    string[] Findings,
    ResearchSource[] Sources,
    string[] ConfidenceNotes,
    string[] UnresolvedQuestions);

/// <summary>
/// Declares a guided self-evolution proposal category.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvolutionProposalCategory>))]
public enum EvolutionProposalCategory
{
    [JsonStringEnumMemberName("promptPolicy")]
    PromptPolicy,

    [JsonStringEnumMemberName("modelRouting")]
    ModelRouting,

    [JsonStringEnumMemberName("approvalDefaults")]
    ApprovalDefaults,

    [JsonStringEnumMemberName("skillSuggestion")]
    SkillSuggestion,

    [JsonStringEnumMemberName("pluginSuggestion")]
    PluginSuggestion,

    [JsonStringEnumMemberName("knowledgeRefresh")]
    KnowledgeRefresh,

    [JsonStringEnumMemberName("codeSpec")]
    CodeSpec,
}

/// <summary>
/// Lifecycle status for a durable evolution proposal.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvolutionProposalStatus>))]
public enum EvolutionProposalStatus
{
    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("applied")]
    Applied,

    [JsonStringEnumMemberName("rejected")]
    Rejected,
}

/// <summary>
/// Durable guided self-evolution proposal.
/// </summary>
public sealed record EvolutionProposal(
    string Id,
    string WorkspaceRoot,
    EvolutionProposalCategory Category,
    EvolutionProposalStatus Status,
    string Title,
    string Summary,
    string[] Evidence,
    string[] RecommendedActions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null,
    string? AppliedBy = null);
