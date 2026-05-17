using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Abstractions;

/// <summary>
/// Adapter for one external agent CLI.
/// </summary>
public interface IExternalAgentAdapter
{
    /// <summary>
    /// Gets the adapter descriptor.
    /// </summary>
    ExternalAgentDescriptor Descriptor { get; }

    /// <summary>
    /// Probes the adapter status.
    /// </summary>
    Task<ExternalAgentStatus> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a prompt through the external agent.
    /// </summary>
    Task<ExternalAgentRunResult> RunAsync(ExternalAgentRunRequest request, CancellationToken cancellationToken);
}
