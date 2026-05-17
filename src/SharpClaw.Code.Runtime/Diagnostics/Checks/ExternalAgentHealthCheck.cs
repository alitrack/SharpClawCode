using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Operational;

namespace SharpClaw.Code.Runtime.Diagnostics.Checks;

/// <summary>
/// Reports configured external agent adapter health.
/// </summary>
public sealed class ExternalAgentHealthCheck(IExternalAgentRegistry registry) : IOperationalCheck
{
    /// <inheritdoc />
    public string Id => "external-agents.registry";

    /// <inheritdoc />
    public async Task<OperationalCheckItem> ExecuteAsync(OperationalDiagnosticsContext context, CancellationToken cancellationToken)
    {
        var report = await registry.BuildReportAsync(context.NormalizedWorkspacePath, cancellationToken).ConfigureAwait(false);
        if (!report.Enabled)
        {
            return new OperationalCheckItem(Id, OperationalCheckStatus.Skipped, "External agents are disabled.", null);
        }

        var available = report.Agents.Count(agent => agent.Health == ExternalAgentHealth.Available);
        var missing = report.Agents.Count(agent => agent.Health == ExternalAgentHealth.Missing);
        var status = missing == report.Agents.Count ? OperationalCheckStatus.Warn : OperationalCheckStatus.Ok;
        var detail = string.Join("; ", report.Agents.Select(agent => $"{agent.Descriptor.Id}: {agent.Health}"));
        return new OperationalCheckItem(
            Id,
            status,
            $"{available}/{report.Agents.Count} external agent adapter(s) available.",
            detail);
    }
}
