using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when a named tool call is absent from the trace.
/// </summary>
public sealed class ToolNotCalledOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "ToolNotCalled";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        if (string.IsNullOrWhiteSpace(definition.ToolName))
        {
            return OracleHelpers.Fail(Name, "ToolNotCalled requires toolName.", "toolName", "missing");
        }

        var calls = OracleHelpers.ToolCalls(trace).ToArray();
        var matching = calls.Where(call => OracleHelpers.ToolMatches(definition.ToolName, call.ToolName)).ToArray();
        return matching.Length == 0
            ? OracleHelpers.Pass(Name, $"Tool '{definition.ToolName}' was not called.", $"No {definition.ToolName} calls", "0")
            : OracleHelpers.Fail(Name, $"Tool '{definition.ToolName}' was called {matching.Length} time(s).", $"No {definition.ToolName} calls", matching.Length.ToString());
    }
}
