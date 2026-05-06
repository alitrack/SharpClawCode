using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Extracts, stores, and applies guided self-evolution proposals.
/// </summary>
public interface IEvolutionProposalService
{
    /// <summary>
    /// Lists proposals for the workspace.
    /// </summary>
    Task<IReadOnlyList<EvolutionProposal>> ListAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a proposal by id.
    /// </summary>
    Task<EvolutionProposal?> GetAsync(string workspaceRoot, string proposalId, CancellationToken cancellationToken);

    /// <summary>
    /// Analyzes workspace and session signals and updates durable proposals.
    /// </summary>
    Task<IReadOnlyList<EvolutionProposal>> AnalyzeAsync(string workspaceRoot, string? sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies one proposal after approval.
    /// </summary>
    Task<EvolutionProposal> ApplyAsync(
        string workspaceRoot,
        string proposalId,
        RuntimeCommandContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rejects one proposal.
    /// </summary>
    Task<EvolutionProposal> RejectAsync(string workspaceRoot, string proposalId, string? rejectedBy, CancellationToken cancellationToken);
}
