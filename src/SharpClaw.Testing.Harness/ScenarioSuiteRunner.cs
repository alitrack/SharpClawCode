using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Runs a directory of scenarios and evaluates gates.
/// </summary>
public sealed class ScenarioSuiteRunner(
    IScenarioLoader loader,
    IScenarioRunner runner,
    ScenarioGateEvaluator gateEvaluator)
{
    /// <summary>
    /// Creates a default suite runner.
    /// </summary>
    /// <param name="traceDirectory">Directory where traces should be written.</param>
    /// <returns>A configured suite runner.</returns>
    public static ScenarioSuiteRunner CreateDefault(string traceDirectory)
        => new(
            new JsonScenarioLoader(),
            ScenarioRunner.CreateDefault(new FileTraceWriter(traceDirectory)),
            new ScenarioGateEvaluator());

    /// <summary>
    /// Runs every JSON scenario in a directory.
    /// </summary>
    /// <param name="directory">Scenario directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Suite result.</returns>
    public async Task<ScenarioSuiteResult> RunDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var scenarios = await loader.LoadDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
        var results = new List<ScenarioRunResult>(scenarios.Count);

        foreach (var scenario in scenarios)
        {
            results.Add(await runner.RunAsync(scenario, cancellationToken).ConfigureAwait(false));
        }

        var gates = gateEvaluator.Evaluate(results);
        return new ScenarioSuiteResult
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Results = results,
            Gates = gates,
            Passed = gates.All(static gate => gate.Passed),
        };
    }
}
