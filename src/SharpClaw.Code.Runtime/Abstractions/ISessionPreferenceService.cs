using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Manages durable session-scoped control-plane preferences such as trust and model selection.
/// </summary>
public interface ISessionPreferenceService
{
    /// <summary>
    /// Gets the effective permission/trust snapshot for a workspace session.
    /// </summary>
    Task<PermissionStatusReport> GetPermissionStatusAsync(
        string workspaceRoot,
        string? sessionId,
        PermissionMode fallbackPermissionMode,
        ApprovalSettings? approvalSettings,
        string? currentModel,
        CancellationToken cancellationToken);

    /// <summary>
    /// Grants durable session trust for one plugin or MCP server.
    /// </summary>
    Task<PermissionStatusReport> GrantTrustAsync(
        string workspaceRoot,
        string? sessionId,
        TrustedSourceKind kind,
        string name,
        PermissionMode fallbackPermissionMode,
        ApprovalSettings? approvalSettings,
        string? currentModel,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes durable session trust for one plugin or MCP server.
    /// </summary>
    Task<PermissionStatusReport> RevokeTrustAsync(
        string workspaceRoot,
        string? sessionId,
        TrustedSourceKind kind,
        string name,
        PermissionMode fallbackPermissionMode,
        ApprovalSettings? approvalSettings,
        string? currentModel,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists the preferred model for a session.
    /// </summary>
    Task<SessionModelPreference> SetModelPreferenceAsync(
        string workspaceRoot,
        string? sessionId,
        string model,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears the persisted model preference for a session.
    /// </summary>
    Task<bool> ClearModelPreferenceAsync(string workspaceRoot, string? sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the preferred permission mode for a session.
    /// </summary>
    Task<PermissionMode> SetPreferredPermissionModeAsync(
        string workspaceRoot,
        string? sessionId,
        PermissionMode permissionMode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists durable auto-approval settings for a session.
    /// </summary>
    Task<ApprovalSettings> SetApprovalSettingsAsync(
        string workspaceRoot,
        string? sessionId,
        ApprovalSettings approvalSettings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears durable auto-approval settings for a session.
    /// </summary>
    Task<bool> ClearApprovalSettingsAsync(string workspaceRoot, string? sessionId, CancellationToken cancellationToken);
}
