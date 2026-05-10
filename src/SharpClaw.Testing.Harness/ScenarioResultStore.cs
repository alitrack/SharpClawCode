using System.Text.Json;
using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Persists and reloads suite results as JSON.
/// </summary>
public sealed class ScenarioResultStore
{
    /// <summary>
    /// Writes a suite result to a JSON file.
    /// </summary>
    /// <param name="result">Suite result.</param>
    /// <param name="path">Destination path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteAsync(ScenarioSuiteResult result, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer
            .SerializeAsync(stream, result, ScenarioJsonContext.Default.ScenarioSuiteResult, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a suite result JSON file.
    /// </summary>
    /// <param name="path">Result path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded suite result.</returns>
    public async Task<ScenarioSuiteResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        var result = await JsonSerializer
            .DeserializeAsync(stream, ScenarioJsonContext.Default.ScenarioSuiteResult, cancellationToken)
            .ConfigureAwait(false);

        return result ?? throw new InvalidDataException($"Result file '{path}' did not contain a suite result.");
    }
}
