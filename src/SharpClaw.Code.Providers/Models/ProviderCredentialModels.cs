namespace SharpClaw.Code.Providers;

/// <summary>
/// Stored user-scoped provider credential descriptor.
/// </summary>
public sealed record ProviderCredentialDescriptor(
    string ProviderName,
    string SourceType,
    string? EnvironmentVariableName,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Resolved provider credential payload for runtime use.
/// </summary>
public sealed record ResolvedProviderCredential(
    string? ApiKey,
    string? SourceType,
    string? StatusDetail);
