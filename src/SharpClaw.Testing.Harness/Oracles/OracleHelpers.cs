using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness.Oracles;

internal static class OracleHelpers
{
    public static IEnumerable<ToolCallTraceStep> ToolCalls(AgentRunTrace trace)
        => trace.Steps
            .Where(static step => step.Kind == TraceStepKind.ToolCall && step.ToolCall is not null)
            .Select(static step => step.ToolCall!);

    public static IEnumerable<ToolResultTraceStep> ToolResults(AgentRunTrace trace)
        => trace.Steps
            .Where(static step => step.Kind == TraceStepKind.ToolResult && step.ToolResult is not null)
            .Select(static step => step.ToolResult!);

    public static bool ToolMatches(string? expectedToolName, string actualToolName)
        => string.IsNullOrWhiteSpace(expectedToolName)
            || string.Equals(expectedToolName, actualToolName, StringComparison.OrdinalIgnoreCase);

    public static OracleResult Pass(string name, string message, string? expected = null, string? actual = null)
        => new()
        {
            OracleName = name,
            Passed = true,
            Message = message,
            Expected = expected,
            Actual = actual,
        };

    public static OracleResult Fail(string name, string message, string? expected = null, string? actual = null)
        => new()
        {
            OracleName = name,
            Passed = false,
            Message = message,
            Expected = expected,
            Actual = actual,
        };
}
