using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.WorkItems.Abstractions;

/// <summary>
/// Session-aware work-item import and export service.
/// </summary>
public interface IWorkItemService
{
    /// <summary>
    /// Imports a work item and links it to a session.
    /// </summary>
    Task<WorkItemImportResult> ImportAsync(WorkItemImportRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Exports a work-item-aware session summary.
    /// </summary>
    Task<WorkItemSummaryExport> ExportSummaryAsync(WorkItemExportRequest request, string workspaceRoot, CancellationToken cancellationToken);
}
