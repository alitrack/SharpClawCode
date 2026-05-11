namespace SharpClaw.Code.Providers.Abstractions;

/// <summary>
/// Resolves and persists user-scoped provider credentials without writing plaintext workspace state.
/// </summary>
public interface IProviderCredentialStore
{
    /// <summary>
    /// Resolves the effective API key for a provider, if available.
    /// </summary>
    Task<ResolvedProviderCredential> ResolveAsync(string providerName, CancellationToken cancellationToken);

    /// <summary>
    /// Lists stored credential descriptors without exposing secret material.
    /// </summary>
    Task<IReadOnlyList<ProviderCredentialDescriptor>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores an environment-variable reference for the provider.
    /// </summary>
    Task SetEnvironmentVariableAsync(string providerName, string environmentVariableName, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a protected secret for the provider when supported on the current platform.
    /// </summary>
    Task SetProtectedSecretAsync(string providerName, string apiKey, CancellationToken cancellationToken);

    /// <summary>
    /// Clears any stored credential reference for the provider.
    /// </summary>
    Task<bool> ClearAsync(string providerName, CancellationToken cancellationToken);
}
