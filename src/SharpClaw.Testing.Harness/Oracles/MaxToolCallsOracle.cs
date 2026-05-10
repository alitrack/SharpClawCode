using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when the total or named tool-call count stays under a maximum.
/// </summary>
public sealed class MaxToolCallsOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "MaxToolCalls";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        if (definition.MaxCount is null or < 0)
        {
            return OracleHelpers.Fail(Name, "MaxToolCalls requires maxCount >= 0.", "maxCount >= 0", definition.MaxCount?.ToString() ?? "missing");
        }

        var calls = OracleHelpers.ToolCalls(trace)
            .Where(call => OracleHelpers.ToolMatches(definition.ToolName, call.ToolName))
            .ToArray();
        var passed = calls.Length <= definition.MaxCount.Value;
        var scope = string.IsNullOrWhiteSpace(definition.ToolName) ? "all tools" : definition.ToolName;
        return passed
            ? OracleHelpers.Pass(Name, $"Tool-call count for {scope} was {calls.Length}.", $"<= {definition.MaxCount}", calls.Length.ToString())
            : OracleHelpers.Fail(Name, $"Tool-call count for {scope} was {calls.Length}.", $"<= {definition.MaxCount}", calls.Length.ToString());
    }
}
