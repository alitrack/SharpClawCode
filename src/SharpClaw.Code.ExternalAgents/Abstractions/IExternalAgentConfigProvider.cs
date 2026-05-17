using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Abstractions;

/// <summary>
/// Supplies merged external agent configuration for a workspace.
/// </summary>
public interface IExternalAgentConfigProvider
{
    /// <summary>
    /// Gets external agent configuration for a workspace.
    /// </summary>
    Task<ExternalAgentsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken);
}
