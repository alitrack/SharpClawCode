using System.Text.Json;
using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Loads scenario definitions from JSON files using source-generated metadata.
/// </summary>
public sealed class JsonScenarioLoader : IScenarioLoader
{
    /// <inheritdoc />
    public async Task<AgentScenario> LoadFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        var scenario = await JsonSerializer
            .DeserializeAsync(stream, ScenarioJsonContext.Default.AgentScenario, cancellationToken)
            .ConfigureAwait(false);

        if (scenario is null)
        {
            throw new InvalidDataException($"Scenario file '{path}' did not contain a valid scenario.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Id))
        {
            throw new InvalidDataException($"Scenario file '{path}' must define a non-empty id.");
        }

        return scenario;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentScenario>> LoadDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scenarios = new List<AgentScenario>(files.Length);

        foreach (var file in files)
        {
            scenarios.Add(await LoadFileAsync(file, cancellationToken).ConfigureAwait(false));
        }

        return scenarios
            .OrderBy(static scenario => scenario.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
