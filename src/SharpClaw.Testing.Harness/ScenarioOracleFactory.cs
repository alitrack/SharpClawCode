using SharpClaw.Testing.Abstractions;
using SharpClaw.Testing.Harness.Oracles;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Creates built-in oracle instances from serializable definitions.
/// </summary>
public sealed class ScenarioOracleFactory
{
    /// <summary>
    /// Creates an oracle for a scenario definition.
    /// </summary>
    /// <param name="definition">Serializable oracle definition.</param>
    /// <returns>The configured oracle.</returns>
    public IScenarioOracle Create(ScenarioOracleDefinition definition)
        => definition.Type switch
        {
            ScenarioOracleType.ToolCalled => new ToolCalledOracle(definition),
            ScenarioOracleType.ToolNotCalled => new ToolNotCalledOracle(definition),
            ScenarioOracleType.FinalAnswerContains => new FinalAnswerContainsOracle(definition),
            ScenarioOracleType.MaxToolCalls => new MaxToolCallsOracle(definition),
            ScenarioOracleType.StateEquals => new StateEqualsOracle(definition),
            ScenarioOracleType.ApprovalRequired => new ApprovalRequiredOracle(definition),
            ScenarioOracleType.NoUnsafeTool => new NoUnsafeToolOracle(definition),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), $"Unsupported oracle type '{definition.Type}'."),
        };
}
