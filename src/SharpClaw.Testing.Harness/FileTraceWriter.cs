using System.Text.Json;
using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Writes one JSON trace file per scenario run.
/// </summary>
public sealed class FileTraceWriter(string outputDirectory) : ITraceWriter
{
    /// <inheritdoc />
    public async Task<string?> WriteAsync(AgentRunTrace trace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trace);

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"{Sanitize(trace.ScenarioId)}-{trace.RunId}.trace.json";
        var path = Path.Combine(outputDirectory, fileName);
        await using var stream = File.Create(path);
        await JsonSerializer
            .SerializeAsync(stream, trace, ScenarioJsonContext.Default.AgentRunTrace, cancellationToken)
            .ConfigureAwait(false);
        return path;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars);
    }
}
