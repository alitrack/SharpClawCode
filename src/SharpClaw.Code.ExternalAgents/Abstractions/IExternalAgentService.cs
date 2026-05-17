using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Abstractions;

/// <summary>
/// Permission-aware external agent execution service.
/// </summary>
public interface IExternalAgentService
{
    /// <summary>
    /// Lists external agent statuses.
    /// </summary>
    Task<ExternalAgentCatalogReport> ListAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Runs an external agent and persists session events.
    /// </summary>
    Task<ExternalAgentRunResult> RunAsync(ExternalAgentRunRequest request, CancellationToken cancellationToken);
}
