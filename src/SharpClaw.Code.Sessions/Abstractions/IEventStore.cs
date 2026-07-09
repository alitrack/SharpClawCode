using SharpClaw.Code.Protocol.Events;

namespace SharpClaw.Code.Sessions.Abstractions;

/// <summary>
/// Persists append-only runtime events.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends a runtime event to the durable event log.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runtimeEvent">The runtime event to append.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AppendAsync(string workspacePath, string sessionId, RuntimeEvent runtimeEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Reads all runtime events for a session.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session runtime events.</returns>
    Task<IReadOnlyList<RuntimeEvent>> ReadAllAsync(string workspacePath, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the latest runtime events for a session in chronological order.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="count">The maximum number of events to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest session runtime events.</returns>
    async Task<IReadOnlyList<RuntimeEvent>> ReadLatestAsync(string workspacePath, string sessionId, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return (await ReadAllAsync(workspacePath, sessionId, cancellationToken).ConfigureAwait(false)).TakeLast(count).ToArray();
    }
}
