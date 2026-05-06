using System.Text.Json;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Providers.Abstractions;

namespace SharpClaw.Code.Providers.Services;

/// <inheritdoc />
public sealed class ProviderCredentialStore(
    IFileSystem fileSystem,
    IUserProfilePaths userProfilePaths,
    IPathService pathService,
    ISecretProtector secretProtector) : IProviderCredentialStore
{
    private const string CredentialsFileName = "credentials.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc />
    public async Task<ResolvedProviderCredential> ResolveAsync(string providerName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!doc.Providers.TryGetValue(providerName, out var entry))
        {
            return new ResolvedProviderCredential(null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(entry.EnvironmentVariableName))
        {
            var value = Environment.GetEnvironmentVariable(entry.EnvironmentVariableName);
            return new ResolvedProviderCredential(
                string.IsNullOrWhiteSpace(value) ? null : value,
                "env",
                $"environment variable {entry.EnvironmentVariableName}");
        }

        if (!string.IsNullOrWhiteSpace(entry.ProtectedSecret))
        {
            return new ResolvedProviderCredential(
                secretProtector.Unprotect(entry.ProtectedSecret),
                "protectedStore",
                "protected local user store");
        }

        return new ResolvedProviderCredential(null, entry.SourceType, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderCredentialDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return doc.Providers
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new ProviderCredentialDescriptor(
                pair.Key,
                pair.Value.SourceType ?? "unknown",
                pair.Value.EnvironmentVariableName,
                pair.Value.UpdatedAtUtc))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task SetEnvironmentVariableAsync(string providerName, string environmentVariableName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);

        var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
        doc.Providers[providerName] = new StoredCredentialEntry(
            SourceType: "env",
            EnvironmentVariableName: environmentVariableName.Trim(),
            ProtectedSecret: null,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        await SaveAsync(doc, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProtectedSecretAsync(string providerName, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!secretProtector.CanProtect)
        {
            throw new InvalidOperationException("Protected local secret storage is unavailable on this platform. Use --env-var instead.");
        }

        var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
        doc.Providers[providerName] = new StoredCredentialEntry(
            SourceType: "protectedStore",
            EnvironmentVariableName: null,
            ProtectedSecret: secretProtector.Protect(apiKey.Trim()),
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        await SaveAsync(doc, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ClearAsync(string providerName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var removed = doc.Providers.Remove(providerName);
        if (removed)
        {
            await SaveAsync(doc, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    private async Task<StoredCredentialDocument> LoadAsync(CancellationToken cancellationToken)
    {
        var path = GetPath();
        var text = await fileSystem.ReadAllTextIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new StoredCredentialDocument(new Dictionary<string, StoredCredentialEntry>(StringComparer.OrdinalIgnoreCase));
        }

        return JsonSerializer.Deserialize<StoredCredentialDocument>(text, JsonOptions)
            ?? new StoredCredentialDocument(new Dictionary<string, StoredCredentialEntry>(StringComparer.OrdinalIgnoreCase));
    }

    private Task SaveAsync(StoredCredentialDocument document, CancellationToken cancellationToken)
        => fileSystem.WriteAllTextAsync(GetPath(), JsonSerializer.Serialize(document, JsonOptions), cancellationToken);

    private string GetPath()
        => pathService.Combine(userProfilePaths.GetUserSharpClawRoot(), CredentialsFileName);

    private sealed record StoredCredentialDocument(
        Dictionary<string, StoredCredentialEntry> Providers);

    private sealed record StoredCredentialEntry(
        string? SourceType,
        string? EnvironmentVariableName,
        string? ProtectedSecret,
        DateTimeOffset UpdatedAtUtc);
}
