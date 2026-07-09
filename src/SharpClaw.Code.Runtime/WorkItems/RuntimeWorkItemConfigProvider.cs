using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Runtime.Abstractions;
using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.Runtime.WorkItems;

/// <summary>
/// Supplies work-item configuration from merged SharpClaw user/workspace config.
/// </summary>
public sealed class RuntimeWorkItemConfigProvider(ISharpClawConfigService configService) : IWorkItemConfigProvider
{
    /// <inheritdoc />
    public async Task<WorkItemsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var snapshot = await configService.GetConfigAsync(workspaceRoot, cancellationToken).ConfigureAwait(false);
        return snapshot.Document.WorkItems ?? new WorkItemsConfig();
    }
}
