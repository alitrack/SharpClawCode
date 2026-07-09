using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Services;

/// <summary>
/// Default external agent configuration used when runtime config is unavailable.
/// </summary>
public sealed class DefaultExternalAgentConfigProvider : IExternalAgentConfigProvider
{
    /// <inheritdoc />
    public Task<ExternalAgentsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        _ = workspaceRoot;
        _ = cancellationToken;
        return Task.FromResult(new ExternalAgentsConfig());
    }
}
