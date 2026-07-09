using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.WorkItems.Abstractions;

/// <summary>
/// Supplies work-item integration configuration for a workspace.
/// </summary>
public interface IWorkItemConfigProvider
{
    /// <summary>
    /// Gets work-item configuration for a workspace.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective work-item configuration.</returns>
    Task<WorkItemsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken);
}
