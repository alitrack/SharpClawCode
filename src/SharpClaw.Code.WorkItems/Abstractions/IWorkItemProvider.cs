using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.WorkItems.Abstractions;

/// <summary>
/// Imports work items from one workflow system.
/// </summary>
public interface IWorkItemProvider
{
    /// <summary>
    /// Gets the provider id.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Returns whether this provider can import the identifier or URL.
    /// </summary>
    bool CanImport(string idOrUrl);

    /// <summary>
    /// Imports a work item.
    /// </summary>
    Task<WorkItem> ImportAsync(WorkItemImportRequest request, CancellationToken cancellationToken);
}
