using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Replays scripted trace steps from a scenario file.
/// </summary>
public sealed class ScriptedScenarioAgentExecutor : IAgentScenarioExecutor
{
    /// <inheritdoc />
    public Task<AgentRunTrace> ExecuteAsync(AgentScenario scenario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(scenario.Input.Executor, "scripted", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Scenario executor '{scenario.Input.Executor}' is not registered. The first harness slice supports 'scripted'.");
        }

        var now = DateTimeOffset.UtcNow;
        var steps = NormalizeSteps(scenario, now);
        var finalState = BuildFinalState(scenario, steps);
        var finalAnswer = steps.LastOrDefault(static step => step.Kind == TraceStepKind.FinalAnswer)?.FinalAnswer
            ?? scenario.Input.ScriptedFinalAnswer;
        var timedOut = scenario.Input.Metadata is not null
            && scenario.Input.Metadata.TryGetValue("timedOut", out var timedOutText)
            && bool.TryParse(timedOutText, out var parsedTimedOut)
            && parsedTimedOut;

        return Task.FromResult(new AgentRunTrace
        {
            RunId = Guid.NewGuid().ToString("N"),
            ScenarioId = scenario.Id,
            StartedAtUtc = now,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            FinalAnswer = finalAnswer,
            TimedOut = timedOut,
            Failed = false,
            Steps = steps,
            FinalState = finalState,
        });
    }

    private static IReadOnlyList<TraceStep> NormalizeSteps(AgentScenario scenario, DateTimeOffset now)
    {
        var scriptedTrace = scenario.Input.ScriptedTrace ?? [];
        var steps = new List<TraceStep>(scriptedTrace.Count + 1);
        var sequence = 1;

        foreach (var step in scriptedTrace)
        {
            steps.Add(step with
            {
                Sequence = step.Sequence > 0 ? step.Sequence : sequence,
                TimestampUtc = step.TimestampUtc == default ? now.AddMilliseconds(sequence) : step.TimestampUtc,
            });
            sequence++;
        }

        if (!string.IsNullOrWhiteSpace(scenario.Input.ScriptedFinalAnswer)
            && !steps.Any(static step => step.Kind == TraceStepKind.FinalAnswer))
        {
            steps.Add(new TraceStep
            {
                Sequence = sequence,
                TimestampUtc = now.AddMilliseconds(sequence),
                Kind = TraceStepKind.FinalAnswer,
                FinalAnswer = scenario.Input.ScriptedFinalAnswer,
            });
        }

        return steps
            .OrderBy(static step => step.Sequence)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildFinalState(AgentScenario scenario, IReadOnlyList<TraceStep> steps)
    {
        var finalState = scenario.Input.ScriptedFinalState is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(scenario.Input.ScriptedFinalState, StringComparer.Ordinal);

        foreach (var stateChange in steps.Select(static step => step.StateChange).Where(static step => step is not null))
        {
            finalState[stateChange!.Key] = stateChange.NewValue;
        }

        return finalState;
    }
}
