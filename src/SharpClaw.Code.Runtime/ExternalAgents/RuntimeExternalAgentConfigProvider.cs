using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Runtime.ExternalAgents;

/// <summary>
/// Supplies external agent configuration from merged SharpClaw user/workspace config.
/// </summary>
public sealed class RuntimeExternalAgentConfigProvider(ISharpClawConfigService configService) : IExternalAgentConfigProvider
{
    /// <inheritdoc />
    public async Task<ExternalAgentsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var snapshot = await configService.GetConfigAsync(workspaceRoot, cancellationToken).ConfigureAwait(false);
        return snapshot.Document.ExternalAgents ?? new ExternalAgentsConfig();
    }
}
