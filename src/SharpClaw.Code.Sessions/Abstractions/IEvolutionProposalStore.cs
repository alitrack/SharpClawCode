using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Sessions.Abstractions;

/// <summary>
/// Persists durable guided self-evolution proposals for one workspace.
/// </summary>
public interface IEvolutionProposalStore
{
    /// <summary>
    /// Lists all evolution proposals for a workspace.
    /// </summary>
    Task<IReadOnlyList<EvolutionProposal>> ListAsync(string workspacePath, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one evolution proposal by id.
    /// </summary>
    Task<EvolutionProposal?> GetByIdAsync(string workspacePath, string proposalId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves one evolution proposal.
    /// </summary>
    Task SaveAsync(string workspacePath, EvolutionProposal proposal, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one evolution proposal.
    /// </summary>
    Task<bool> DeleteAsync(string workspacePath, string proposalId, CancellationToken cancellationToken);
}
