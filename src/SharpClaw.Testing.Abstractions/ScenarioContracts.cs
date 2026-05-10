using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpClaw.Testing.Abstractions;

/// <summary>
/// Describes the severity and release-gating importance of a scenario.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScenarioRisk>))]
public enum ScenarioRisk
{
    /// <summary>
    /// The scenario covers a low-impact behavior.
    /// </summary>
    Low,

    /// <summary>
    /// The scenario covers normal product behavior.
    /// </summary>
    Medium,

    /// <summary>
    /// The scenario covers behavior that should fail the gate when broken.
    /// </summary>
    High,

    /// <summary>
    /// The scenario covers a critical safety or reliability invariant.
    /// </summary>
    Critical,
}

/// <summary>
/// Identifies a trace step payload shape.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TraceStepKind>))]
public enum TraceStepKind
{
    /// <summary>
    /// A human-readable trace message.
    /// </summary>
    Message,

    /// <summary>
    /// An attempted tool call.
    /// </summary>
    ToolCall,

    /// <summary>
    /// A returned tool result.
    /// </summary>
    ToolResult,

    /// <summary>
    /// A named state transition.
    /// </summary>
    StateChange,

    /// <summary>
    /// A final answer emitted by the agent.
    /// </summary>
    FinalAnswer,
}

/// <summary>
/// Identifies the built-in oracle implementation selected by a scenario file.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScenarioOracleType>))]
public enum ScenarioOracleType
{
    /// <summary>
    /// Requires a matching tool call to be present.
    /// </summary>
    ToolCalled,

    /// <summary>
    /// Requires a matching tool call to be absent.
    /// </summary>
    ToolNotCalled,

    /// <summary>
    /// Requires the final answer to contain text.
    /// </summary>
    FinalAnswerContains,

    /// <summary>
    /// Requires the total or named tool-call count to stay under a limit.
    /// </summary>
    MaxToolCalls,

    /// <summary>
    /// Requires a named final-state value.
    /// </summary>
    StateEquals,

    /// <summary>
    /// Requires approval to be requested for a matching tool call.
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// Requires unsafe tool calls to be blocked before execution.
    /// </summary>
    NoUnsafeTool,
}

/// <summary>
/// A complete scenario with explicit input, risk, and expected outcomes.
/// </summary>
public sealed record AgentScenario
{
    /// <summary>
    /// Stable identifier used in reports, traces, and xUnit display names.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name shown in reports.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional scenario description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Scenario risk used by release gates.
    /// </summary>
    public ScenarioRisk Risk { get; init; } = ScenarioRisk.Medium;

    /// <summary>
    /// Tags used for filtering and reporting.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Input passed to the selected scenario executor.
    /// </summary>
    public required ScenarioInput Input { get; init; }

    /// <summary>
    /// Explicit expected outcomes and oracle definitions.
    /// </summary>
    public required ScenarioExpected Expected { get; init; }
}

/// <summary>
/// Input supplied to the scenario executor.
/// </summary>
public sealed record ScenarioInput
{
    /// <summary>
    /// Prompt or task text supplied to the agent or adapter.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Executor id. The initial implementation supports <c>scripted</c>.
    /// </summary>
    public string Executor { get; init; } = "scripted";

    /// <summary>
    /// Optional working directory override relative to the runner workspace.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Optional timeout budget in milliseconds.
    /// </summary>
    public int? TimeoutMilliseconds { get; init; }

    /// <summary>
    /// Metadata passed through to future runtime adapters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Scripted trace used by the first replay-style executor.
    /// </summary>
    public IReadOnlyList<TraceStep> ScriptedTrace { get; init; } = [];

    /// <summary>
    /// Scripted final answer used when no final-answer trace step is present.
    /// </summary>
    public string? ScriptedFinalAnswer { get; init; }

    /// <summary>
    /// Scripted final state used when state changes are not enough to derive state.
    /// </summary>
    public IReadOnlyDictionary<string, string> ScriptedFinalState { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Expected scenario outcomes expressed as explicit oracle definitions.
/// </summary>
public sealed record ScenarioExpected
{
    /// <summary>
    /// Oracle definitions that must be evaluated for the run.
    /// </summary>
    public IReadOnlyList<ScenarioOracleDefinition> Oracles { get; init; } = [];

    /// <summary>
    /// Forces this scenario to participate in gates regardless of risk.
    /// </summary>
    public bool RequiredForGates { get; init; }
}

/// <summary>
/// Serializable configuration for a built-in oracle.
/// </summary>
public sealed record ScenarioOracleDefinition
{
    /// <summary>
    /// Oracle type to instantiate.
    /// </summary>
    public required ScenarioOracleType Type { get; init; }

    /// <summary>
    /// Optional human-readable oracle label.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Tool name used by tool-related oracles.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Text expected in the final answer.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Maximum allowed tool-call count.
    /// </summary>
    public int? MaxCount { get; init; }

    /// <summary>
    /// State key used by state equality oracles.
    /// </summary>
    public string? StateKey { get; init; }

    /// <summary>
    /// Expected state value.
    /// </summary>
    public string? ExpectedValue { get; init; }

    /// <summary>
    /// Whether string checks should be case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Optional explanation shown in reports.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Trace produced by one scenario run.
/// </summary>
public sealed record AgentRunTrace
{
    /// <summary>
    /// Unique run identifier.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Scenario id that produced this trace.
    /// </summary>
    public required string ScenarioId { get; init; }

    /// <summary>
    /// UTC start timestamp.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// UTC completion timestamp.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// Final answer emitted by the executor.
    /// </summary>
    public string? FinalAnswer { get; init; }

    /// <summary>
    /// True when the executor reported a timeout.
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// True when the executor failed before oracle evaluation.
    /// </summary>
    public bool Failed { get; init; }

    /// <summary>
    /// Executor error message, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Ordered trace steps.
    /// </summary>
    public IReadOnlyList<TraceStep> Steps { get; init; } = [];

    /// <summary>
    /// Final named state values for state oracles.
    /// </summary>
    public IReadOnlyDictionary<string, string> FinalState { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// A single ordered trace entry. Payloads are explicit properties rather than polymorphic JSON.
/// </summary>
public sealed record TraceStep
{
    /// <summary>
    /// One-based step sequence.
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// UTC timestamp for the step.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Step kind.
    /// </summary>
    public TraceStepKind Kind { get; init; } = TraceStepKind.Message;

    /// <summary>
    /// Optional human-readable trace message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Tool-call payload for <see cref="TraceStepKind.ToolCall"/>.
    /// </summary>
    public ToolCallTraceStep? ToolCall { get; init; }

    /// <summary>
    /// Tool-result payload for <see cref="TraceStepKind.ToolResult"/>.
    /// </summary>
    public ToolResultTraceStep? ToolResult { get; init; }

    /// <summary>
    /// State-change payload for <see cref="TraceStepKind.StateChange"/>.
    /// </summary>
    public StateChangeTraceStep? StateChange { get; init; }

    /// <summary>
    /// Final-answer payload for <see cref="TraceStepKind.FinalAnswer"/>.
    /// </summary>
    public string? FinalAnswer { get; init; }
}

/// <summary>
/// Captures an attempted tool call.
/// </summary>
public sealed record ToolCallTraceStep
{
    /// <summary>
    /// Stable call id used to pair calls with results.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Tool name.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Raw JSON argument payload, if captured.
    /// </summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>
    /// True when the call required explicit approval.
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// True when the tool is considered unsafe unless blocked or approved.
    /// </summary>
    public bool IsUnsafe { get; init; }

    /// <summary>
    /// True when the unsafe or approval-sensitive call was blocked before execution.
    /// </summary>
    public bool WasBlocked { get; init; }
}

/// <summary>
/// Captures a tool result.
/// </summary>
public sealed record ToolResultTraceStep
{
    /// <summary>
    /// Stable call id paired with a tool call.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Tool name.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// True when the tool completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Captured tool output.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Captured error message.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Captures a named state transition.
/// </summary>
public sealed record StateChangeTraceStep
{
    /// <summary>
    /// State key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// State value before the transition.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// State value after the transition.
    /// </summary>
    public required string NewValue { get; init; }
}

/// <summary>
/// Result produced by one oracle evaluation.
/// </summary>
public sealed record OracleResult
{
    /// <summary>
    /// Oracle display name.
    /// </summary>
    public required string OracleName { get; init; }

    /// <summary>
    /// True when the oracle passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Clear result message for failures and reports.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Expected value summary.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Actual value summary.
    /// </summary>
    public string? Actual { get; init; }
}

/// <summary>
/// Full result for one scenario run.
/// </summary>
public sealed record ScenarioRunResult
{
    /// <summary>
    /// Scenario that was executed.
    /// </summary>
    public required AgentScenario Scenario { get; init; }

    /// <summary>
    /// Captured run trace.
    /// </summary>
    public required AgentRunTrace Trace { get; init; }

    /// <summary>
    /// Oracle evaluation results.
    /// </summary>
    public IReadOnlyList<OracleResult> OracleResults { get; init; } = [];

    /// <summary>
    /// True when the executor succeeded and every oracle passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Trace file path written by the runner, if any.
    /// </summary>
    public string? TracePath { get; init; }
}

/// <summary>
/// Gate outcome for a suite run.
/// </summary>
public sealed record ScenarioGateResult
{
    /// <summary>
    /// Gate name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// True when the gate passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gate outcome message.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Full result for a scenario suite run.
/// </summary>
public sealed record ScenarioSuiteResult
{
    /// <summary>
    /// UTC timestamp for the suite execution.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>
    /// Scenario run results.
    /// </summary>
    public IReadOnlyList<ScenarioRunResult> Results { get; init; } = [];

    /// <summary>
    /// Gate evaluation results.
    /// </summary>
    public IReadOnlyList<ScenarioGateResult> Gates { get; init; } = [];

    /// <summary>
    /// True when every scenario and gate passed.
    /// </summary>
    public bool Passed { get; init; }
}

/// <summary>
/// Source-generated JSON metadata for scenario and result contracts.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AgentScenario))]
[JsonSerializable(typeof(IReadOnlyList<AgentScenario>))]
[JsonSerializable(typeof(AgentRunTrace))]
[JsonSerializable(typeof(ScenarioRunResult))]
[JsonSerializable(typeof(ScenarioSuiteResult))]
public partial class ScenarioJsonContext : JsonSerializerContext;
