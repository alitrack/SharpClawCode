using SharpClaw.Code.Protocol.Abstractions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Sessions.Abstractions;

namespace SharpClaw.Code.Sessions.Storage;

/// <summary>
/// Selects the effective scheduled-prompt backend from the active host context.
/// </summary>
public sealed class HostAwareScheduledPromptStore(
    FileScheduledPromptStore fileStore,
    SqliteScheduledPromptStore sqliteStore,
    IRuntimeHostContextAccessor hostContextAccessor) : IScheduledPromptStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduledPromptDefinition>> ListAsync(string workspacePath, CancellationToken cancellationToken)
        => ResolveStore().ListAsync(workspacePath, cancellationToken);

    /// <inheritdoc />
    public Task<ScheduledPromptDefinition?> GetByIdAsync(string workspacePath, string scheduleId, CancellationToken cancellationToken)
        => ResolveStore().GetByIdAsync(workspacePath, scheduleId, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(string workspacePath, ScheduledPromptDefinition definition, CancellationToken cancellationToken)
        => ResolveStore().SaveAsync(workspacePath, definition, cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string workspacePath, string scheduleId, CancellationToken cancellationToken)
        => ResolveStore().DeleteAsync(workspacePath, scheduleId, cancellationToken);

    private IScheduledPromptStore ResolveStore()
        => hostContextAccessor.Current?.SessionStoreKind == SessionStoreKind.Sqlite
            ? sqliteStore
            : fileStore;
}
