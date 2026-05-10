using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Trace writer that intentionally persists nothing.
/// </summary>
public sealed class NullTraceWriter : ITraceWriter
{
    /// <summary>
    /// Singleton no-op trace writer.
    /// </summary>
    public static NullTraceWriter Instance { get; } = new();

    private NullTraceWriter()
    {
    }

    /// <inheritdoc />
    public Task<string?> WriteAsync(AgentRunTrace trace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trace);
        _ = cancellationToken;
        return Task.FromResult<string?>(null);
    }
}
