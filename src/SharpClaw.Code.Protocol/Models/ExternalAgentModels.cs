using System.Text.Json;
using System.Text.Json.Serialization;
using SharpClaw.Code.Protocol.Enums;

namespace SharpClaw.Code.Protocol.Models;

/// <summary>
/// External agent execution mode.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalAgentMode>))]
public enum ExternalAgentMode
{
    /// <summary>Read-oriented prompt execution.</summary>
    ReadOnly,

    /// <summary>Workspace-mutating prompt execution.</summary>
    WorkspaceWrite,
}

/// <summary>
/// Coarse external agent health state.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalAgentHealth>))]
public enum ExternalAgentHealth
{
    /// <summary>The adapter is disabled by configuration.</summary>
    Disabled,

    /// <summary>The configured executable is available.</summary>
    Available,

    /// <summary>The configured executable is missing.</summary>
    Missing,

    /// <summary>The adapter probe failed unexpectedly.</summary>
    Faulted,
}

/// <summary>
/// Failure classification for external agent execution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalAgentFailureKind>))]
public enum ExternalAgentFailureKind
{
    /// <summary>No failure was recorded.</summary>
    None,

    /// <summary>The requested adapter is not registered.</summary>
    UnknownAdapter,

    /// <summary>The adapter or external-agent feature is disabled.</summary>
    Disabled,

    /// <summary>The executable could not be found.</summary>
    ExecutableMissing,

    /// <summary>Permission policy blocked execution.</summary>
    PermissionDenied,

    /// <summary>The process exited unsuccessfully.</summary>
    ProcessFailed,

    /// <summary>The operation was cancelled.</summary>
    Cancelled,

    /// <summary>An unexpected runtime error occurred.</summary>
    Unexpected,
}

/// <summary>
/// Describes one external agent adapter.
/// </summary>
public sealed record ExternalAgentDescriptor(
    string Id,
    string DisplayName,
    string ExecutableName,
    IReadOnlyList<ExternalAgentMode> SupportedModes,
    bool SupportsStreaming,
    bool SupportsJson,
    bool SupportsWorkspace,
    bool SupportsResume,
    IReadOnlyList<string> Capabilities);

/// <summary>
/// Resolved external agent status.
/// </summary>
public sealed record ExternalAgentStatus(
    ExternalAgentDescriptor Descriptor,
    ExternalAgentHealth Health,
    bool Enabled,
    string? ExecutablePath,
    string? Detail);

/// <summary>
/// External agent execution request.
/// </summary>
public sealed record ExternalAgentRunRequest(
    string AdapterId,
    string WorkspacePath,
    string Prompt,
    ExternalAgentMode Mode,
    string? SessionId = null,
    IReadOnlyDictionary<string, string?>? Environment = null,
    IReadOnlyList<string>? AdditionalArgs = null,
    PermissionMode PermissionMode = PermissionMode.WorkspaceWrite,
    PrimaryMode PrimaryMode = PrimaryMode.Build,
    bool IsInteractive = true);

/// <summary>
/// Structured event captured from an external agent.
/// </summary>
public sealed record ExternalAgentEvent(
    DateTimeOffset TimestampUtc,
    string AdapterId,
    string EventType,
    JsonElement? Payload);

/// <summary>
/// External agent execution result.
/// </summary>
public sealed record ExternalAgentRunResult(
    string AdapterId,
    int ExitCode,
    string OutputText,
    IReadOnlyList<ExternalAgentEvent> StructuredEvents,
    string? ExternalSessionId,
    ExternalAgentFailureKind FailureKind,
    string? Error);

/// <summary>
/// Configures one external agent adapter.
/// </summary>
public sealed record ExternalAgentAdapterConfig(
    bool? Enabled = null,
    string? ExecutablePath = null,
    string[]? DefaultArgs = null,
    string? WorkingDirectoryMode = null);

/// <summary>
/// Configures external agent support.
/// </summary>
public sealed record ExternalAgentsConfig(
    bool Enabled = false,
    Dictionary<string, ExternalAgentAdapterConfig>? Adapters = null,
    bool RequireApprovalForMutatingRuns = true);

/// <summary>
/// Command payload for external agent lists and status.
/// </summary>
public sealed record ExternalAgentCatalogReport(
    bool Enabled,
    bool RequireApprovalForMutatingRuns,
    IReadOnlyList<ExternalAgentStatus> Agents);
