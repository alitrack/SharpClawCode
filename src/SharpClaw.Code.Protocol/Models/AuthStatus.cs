namespace SharpClaw.Code.Protocol.Models;

/// <summary>
/// Represents the current authentication status for a provider, plugin, or runtime principal.
/// </summary>
/// <param name="SubjectId">The authenticated subject identifier, if any.</param>
/// <param name="IsAuthenticated">Indicates whether authentication is currently valid.</param>
/// <param name="ProviderName">The authentication provider name, if any.</param>
/// <param name="OrganizationId">The related organization or tenant identifier, if any.</param>
/// <param name="ExpiresAtUtc">The UTC expiration timestamp, if known.</param>
/// <param name="GrantedScopes">The granted scopes or permissions associated with the status.</param>
/// <param name="SourceType">Where the active auth material came from.</param>
/// <param name="StatusDetail">Optional auth detail suitable for CLI/status output.</param>
/// <param name="IsLocalRuntime">Whether the status describes a local runtime profile that may not require credentials.</param>
public sealed record AuthStatus(
    string? SubjectId,
    bool IsAuthenticated,
    string? ProviderName,
    string? OrganizationId,
    DateTimeOffset? ExpiresAtUtc,
    string[]? GrantedScopes,
    string? SourceType = null,
    string? StatusDetail = null,
    bool IsLocalRuntime = false);
