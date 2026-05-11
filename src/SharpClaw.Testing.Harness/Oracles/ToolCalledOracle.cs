using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when a named tool call appears in the trace.
/// </summary>
public sealed class ToolCalledOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "ToolCalled";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        if (string.IsNullOrWhiteSpace(definition.ToolName))
        {
            return OracleHelpers.Fail(Name, "ToolCalled requires toolName.", "toolName", "missing");
        }

        var calls = OracleHelpers.ToolCalls(trace).ToArray();
        var passed = calls.Any(call => OracleHelpers.ToolMatches(definition.ToolName, call.ToolName));
        return passed
            ? OracleHelpers.Pass(Name, $"Tool '{definition.ToolName}' was called.", definition.ToolName, string.Join(", ", calls.Select(static call => call.ToolName)))
            : OracleHelpers.Fail(Name, $"Tool '{definition.ToolName}' was not called.", definition.ToolName, string.Join(", ", calls.Select(static call => call.ToolName)));
    }
}
