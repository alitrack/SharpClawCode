using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

/// <summary>
/// Passes when a matching tool call required explicit approval.
/// </summary>
public sealed class ApprovalRequiredOracle(ScenarioOracleDefinition definition) : IScenarioOracle
{
    /// <inheritdoc />
    public string Name => definition.Name ?? "ApprovalRequired";

    /// <inheritdoc />
    public OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace)
    {
        _ = scenario;
        var matching = OracleHelpers.ToolCalls(trace)
            .Where(call => OracleHelpers.ToolMatches(definition.ToolName, call.ToolName))
            .ToArray();
        var approved = matching.Where(static call => call.RequiresApproval).ToArray();
        var scope = string.IsNullOrWhiteSpace(definition.ToolName) ? "any tool" : definition.ToolName;

        return approved.Length > 0
            ? OracleHelpers.Pass(Name, $"Approval was required for {scope}.", "approval required", approved.Length.ToString())
            : OracleHelpers.Fail(Name, $"Approval was not required for {scope}.", "approval required", matching.Length == 0 ? "no matching tool call" : "not required");
    }
}
