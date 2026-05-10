using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when the final answer contains expected text.
/// </summary>
public sealed class FinalAnswerContainsOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "FinalAnswerContains";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        if (string.IsNullOrEmpty(definition.Text))
        {
            return OracleHelpers.Fail(Name, "FinalAnswerContains requires text.", "text", "missing");
        }

        var finalAnswer = trace.FinalAnswer ?? string.Empty;
        var comparison = definition.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var passed = finalAnswer.Contains(definition.Text, comparison);
        return passed
            ? OracleHelpers.Pass(Name, $"Final answer contained '{definition.Text}'.", definition.Text, finalAnswer)
            : OracleHelpers.Fail(Name, $"Final answer did not contain '{definition.Text}'.", definition.Text, finalAnswer);
    }
}
