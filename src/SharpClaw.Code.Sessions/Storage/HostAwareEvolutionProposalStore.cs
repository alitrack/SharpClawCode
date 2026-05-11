using SharpClaw.Code.Protocol.Abstractions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Sessions.Abstractions;

namespace SharpClaw.Code.Sessions.Storage;

/// <summary>
/// Selects the effective evolution-proposal backend from the active host context.
/// </summary>
public sealed class HostAwareEvolutionProposalStore(
    FileEvolutionProposalStore fileStore,
    SqliteEvolutionProposalStore sqliteStore,
    IRuntimeHostContextAccessor hostContextAccessor) : IEvolutionProposalStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<EvolutionProposal>> ListAsync(string workspacePath, CancellationToken cancellationToken)
        => ResolveStore().ListAsync(workspacePath, cancellationToken);

    /// <inheritdoc />
    public Task<EvolutionProposal?> GetByIdAsync(string workspacePath, string proposalId, CancellationToken cancellationToken)
        => ResolveStore().GetByIdAsync(workspacePath, proposalId, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(string workspacePath, EvolutionProposal proposal, CancellationToken cancellationToken)
        => ResolveStore().SaveAsync(workspacePath, proposal, cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string workspacePath, string proposalId, CancellationToken cancellationToken)
        => ResolveStore().DeleteAsync(workspacePath, proposalId, cancellationToken);

    private IEvolutionProposalStore ResolveStore()
        => hostContextAccessor.Current?.SessionStoreKind == SessionStoreKind.Sqlite
            ? sqliteStore
            : fileStore;
}
