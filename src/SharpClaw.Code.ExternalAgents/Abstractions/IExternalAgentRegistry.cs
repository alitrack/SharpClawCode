using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Abstractions;

/// <summary>
/// Registry of available external agent adapters.
/// </summary>
public interface IExternalAgentRegistry
{
    /// <summary>
    /// Lists registered adapters.
    /// </summary>
    IReadOnlyList<IExternalAgentAdapter> ListAdapters();

    /// <summary>
    /// Resolves an adapter by id.
    /// </summary>
    IExternalAgentAdapter? Resolve(string adapterId);

    /// <summary>
    /// Builds a status report.
    /// </summary>
    Task<ExternalAgentCatalogReport> BuildReportAsync(string workspaceRoot, CancellationToken cancellationToken);
}
