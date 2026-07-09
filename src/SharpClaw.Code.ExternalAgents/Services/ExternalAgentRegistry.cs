using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Services;

/// <summary>
/// Default in-process external agent adapter registry.
/// </summary>
public sealed class ExternalAgentRegistry(
    IEnumerable<IExternalAgentAdapter> adapters,
    IExternalAgentConfigProvider configProvider) : IExternalAgentRegistry
{
    private readonly IExternalAgentAdapter[] orderedAdapters = adapters.OrderBy(adapter => adapter.Descriptor.Id, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<IExternalAgentAdapter> ListAdapters() => orderedAdapters;

    /// <inheritdoc />
    public IExternalAgentAdapter? Resolve(string adapterId)
        => orderedAdapters.FirstOrDefault(adapter => string.Equals(adapter.Descriptor.Id, adapterId, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async Task<ExternalAgentCatalogReport> BuildReportAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var config = await configProvider.GetConfigAsync(workspaceRoot, cancellationToken).ConfigureAwait(false);
        var statuses = new List<ExternalAgentStatus>(orderedAdapters.Length);
        foreach (var adapter in orderedAdapters)
        {
            try
            {
                statuses.Add(await adapter.GetStatusAsync(workspaceRoot, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                statuses.Add(new ExternalAgentStatus(
                    adapter.Descriptor,
                    ExternalAgentHealth.Faulted,
                    Enabled: true,
                    ExecutablePath: null,
                    Detail: ex.ToString()));
            }
        }

        return new ExternalAgentCatalogReport(config.Enabled, config.RequireApprovalForMutatingRuns, statuses);
    }
}
