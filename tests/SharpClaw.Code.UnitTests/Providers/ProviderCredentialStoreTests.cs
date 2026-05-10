using FluentAssertions;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Infrastructure.Services;
using SharpClaw.Code.Providers.Services;

namespace SharpClaw.Code.UnitTests.Providers;

/// <summary>
/// Verifies local provider credential persistence behavior.
/// </summary>
public sealed class ProviderCredentialStoreTests
{
    /// <summary>
    /// Ensures malformed credential state does not permanently break auth commands.
    /// </summary>
    [Fact]
    public async Task LoadAsync_should_recover_from_malformed_credentials_json()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpclaw-credential-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "credentials.json"), "{ malformed json");

        var store = CreateStore(root);

        var descriptors = await store.ListAsync(CancellationToken.None);
        var resolved = await store.ResolveAsync("openai", CancellationToken.None);

        descriptors.Should().BeEmpty();
        resolved.ApiKey.Should().BeNull();
    }

    private static ProviderCredentialStore CreateStore(string root)
    {
        var pathService = new PathService();
        return new ProviderCredentialStore(
            new LocalFileSystem(),
            new FixedUserProfilePaths(root),
            pathService,
            new TestSecretProtector());
    }

    private sealed class FixedUserProfilePaths(string root) : IUserProfilePaths
    {
        public string GetUserHomeDirectory() => root;

        public string GetUserSharpClawRoot() => root;

        public string GetUserCustomCommandsDirectory() => Path.Combine(root, "commands");
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        public bool CanProtect => true;

        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }
}
