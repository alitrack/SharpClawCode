using SharpClaw.Code.Protocol.Operational;

namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Builds static workbench status reports for CLI and REPL views.
/// </summary>
public interface IWorkbenchStatusService
{
    /// <summary>
    /// Builds a workbench status report.
    /// </summary>
    Task<WorkbenchStatusReport> BuildAsync(RuntimeCommandContext context, CancellationToken cancellationToken);
}
