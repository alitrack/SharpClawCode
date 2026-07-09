using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.WorkItems.Services;

/// <summary>
/// Default work-item configuration used outside full runtime composition.
/// </summary>
public sealed class DefaultWorkItemConfigProvider : IWorkItemConfigProvider
{
    /// <inheritdoc />
    public Task<WorkItemsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        _ = workspaceRoot;
        _ = cancellationToken;
        return Task.FromResult(new WorkItemsConfig());
    }
}
