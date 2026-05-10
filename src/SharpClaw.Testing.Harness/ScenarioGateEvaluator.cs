using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Evaluates release gates over scenario run results.
/// </summary>
public sealed class ScenarioGateEvaluator
{
    /// <summary>
    /// Evaluates the default quality gates.
    /// </summary>
    /// <param name="results">Scenario results.</param>
    /// <returns>Gate results.</returns>
    public IReadOnlyList<ScenarioGateResult> Evaluate(IReadOnlyList<ScenarioRunResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return
        [
            EvaluateScenarioDiscoveryGate(results),
            EvaluateExplicitOracleGate(results),
            EvaluateHighRiskGate(results),
            EvaluateRequiredScenarioGate(results),
            EvaluateTracePresenceGate(results),
        ];
    }

    private static ScenarioGateResult EvaluateScenarioDiscoveryGate(IReadOnlyList<ScenarioRunResult> results)
        => results.Count > 0
            ? Pass("scenario-discovery", $"Discovered {results.Count} scenario(s).")
            : Fail("scenario-discovery", "No scenarios were discovered.");

    private static ScenarioGateResult EvaluateExplicitOracleGate(IReadOnlyList<ScenarioRunResult> results)
    {
        var missing = results
            .Where(static result => result.Scenario.Expected.Oracles.Count == 0)
            .Select(static result => result.Scenario.Id)
            .ToArray();

        return missing.Length == 0
            ? Pass("explicit-oracles", "Every scenario defines at least one explicit oracle.")
            : Fail("explicit-oracles", $"Scenarios missing explicit oracles: {string.Join(", ", missing)}.");
    }

    private static ScenarioGateResult EvaluateHighRiskGate(IReadOnlyList<ScenarioRunResult> results)
    {
        var failed = results
            .Where(static result => (result.Scenario.Risk is ScenarioRisk.High or ScenarioRisk.Critical) && !result.Passed)
            .Select(static result => result.Scenario.Id)
            .ToArray();

        return failed.Length == 0
            ? Pass("high-risk-pass", "All high and critical risk scenarios passed.")
            : Fail("high-risk-pass", $"High or critical risk scenarios failed: {string.Join(", ", failed)}.");
    }

    private static ScenarioGateResult EvaluateRequiredScenarioGate(IReadOnlyList<ScenarioRunResult> results)
    {
        var failed = results
            .Where(static result => result.Scenario.Expected.RequiredForGates && !result.Passed)
            .Select(static result => result.Scenario.Id)
            .ToArray();

        return failed.Length == 0
            ? Pass("required-scenarios-pass", "All scenarios marked required for gates passed.")
            : Fail("required-scenarios-pass", $"Required gate scenarios failed: {string.Join(", ", failed)}.");
    }

    private static ScenarioGateResult EvaluateTracePresenceGate(IReadOnlyList<ScenarioRunResult> results)
    {
        var missing = results
            .Where(static result => result.Trace.Steps.Count == 0)
            .Select(static result => result.Scenario.Id)
            .ToArray();

        return missing.Length == 0
            ? Pass("trace-presence", "Every scenario produced at least one trace step.")
            : Fail("trace-presence", $"Scenarios with empty traces: {string.Join(", ", missing)}.");
    }

    private static ScenarioGateResult Pass(string name, string message)
        => new()
        {
            Name = name,
            Passed = true,
            Message = message,
        };

    private static ScenarioGateResult Fail(string name, string message)
        => new()
        {
            Name = name,
            Passed = false,
            Message = message,
        };
}
