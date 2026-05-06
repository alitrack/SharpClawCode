namespace SharpClaw.Code.Infrastructure.Abstractions;

/// <summary>
/// Protects and restores user-scoped secrets for local machine storage.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Gets whether the current platform can persist protected secrets locally.
    /// </summary>
    bool CanProtect { get; }

    /// <summary>
    /// Protects a plaintext secret for the current user.
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Restores a previously protected secret for the current user.
    /// </summary>
    string Unprotect(string protectedPayload);
}
