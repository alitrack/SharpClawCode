using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when a named final-state value equals the expected value.
/// </summary>
public sealed class StateEqualsOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "StateEquals";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        if (string.IsNullOrWhiteSpace(definition.StateKey))
        {
            return OracleHelpers.Fail(Name, "StateEquals requires stateKey.", "stateKey", "missing");
        }

        if (definition.ExpectedValue is null)
        {
            return OracleHelpers.Fail(Name, "StateEquals requires expectedValue.", "expectedValue", "missing");
        }

        var actual = ResolveStateValue(trace, definition.StateKey);
        var passed = string.Equals(actual, definition.ExpectedValue, StringComparison.Ordinal);
        return passed
            ? OracleHelpers.Pass(Name, $"State '{definition.StateKey}' matched.", definition.ExpectedValue, actual)
            : OracleHelpers.Fail(Name, $"State '{definition.StateKey}' did not match.", definition.ExpectedValue, actual ?? "missing");
    }

    private static string? ResolveStateValue(AgentRunTrace trace, string key)
    {
        if (trace.FinalState.TryGetValue(key, out var value))
        {
            return value;
        }

        return trace.Steps
            .Select(static step => step.StateChange)
            .Where(step => step is not null && string.Equals(step.Key, key, StringComparison.Ordinal))
            .LastOrDefault()
            ?.NewValue;
    }
}
