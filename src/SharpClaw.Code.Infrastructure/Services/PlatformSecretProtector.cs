using System.Security.Cryptography;
using System.Text;
using SharpClaw.Code.Infrastructure.Abstractions;

namespace SharpClaw.Code.Infrastructure.Services;

/// <inheritdoc />
public sealed class PlatformSecretProtector : ISecretProtector
{
    /// <inheritdoc />
    public bool CanProtect => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        if (!CanProtect)
        {
            throw new InvalidOperationException("Protected local secret storage is only available on Windows.");
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToBase64String(ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser));
    }

    /// <inheritdoc />
    public string Unprotect(string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedPayload);
        if (!CanProtect)
        {
            throw new InvalidOperationException("Protected local secret storage is only available on Windows.");
        }

        var bytes = Convert.FromBase64String(protectedPayload);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser));
    }
}
