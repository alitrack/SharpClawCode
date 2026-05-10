using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Runs one scenario through an executor and evaluates its explicit oracles.
/// </summary>
public sealed class ScenarioRunner(
    IAgentScenarioExecutor executor,
    ScenarioOracleFactory oracleFactory,
    ITraceWriter traceWriter) : IScenarioRunner
{
    /// <summary>
    /// Creates a default runner backed by the scripted executor.
    /// </summary>
    /// <param name="traceWriter">Optional trace writer.</param>
    /// <returns>A configured runner.</returns>
    public static ScenarioRunner CreateDefault(ITraceWriter? traceWriter = null)
        => new(new ScriptedScenarioAgentExecutor(), new ScenarioOracleFactory(), traceWriter ?? NullTraceWriter.Instance);

    /// <inheritdoc />
    public async Task<ScenarioRunResult> RunAsync(AgentScenario scenario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        AgentRunTrace trace;
        try
        {
            trace = await executor.ExecuteAsync(scenario, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var now = DateTimeOffset.UtcNow;
            trace = new AgentRunTrace
            {
                RunId = Guid.NewGuid().ToString("N"),
                ScenarioId = scenario.Id,
                StartedAtUtc = now,
                CompletedAtUtc = now,
                Failed = true,
                ErrorMessage = exception.Message,
                Steps =
                [
                    new TraceStep
                    {
                        Sequence = 1,
                        TimestampUtc = now,
                        Kind = TraceStepKind.Message,
                        Message = exception.Message,
                    }
                ],
            };
        }

        var tracePath = await traceWriter.WriteAsync(trace, cancellationToken).ConfigureAwait(false);
        var oracleResults = EvaluateOracles(scenario, trace);
        var passed = !trace.Failed
            && oracleResults.Count > 0
            && oracleResults.All(static result => result.Passed);

        return new ScenarioRunResult
        {
            Scenario = scenario,
            Trace = trace,
            TracePath = tracePath,
            OracleResults = oracleResults,
            Passed = passed,
        };
    }

    private IReadOnlyList<OracleResult> EvaluateOracles(AgentScenario scenario, AgentRunTrace trace)
    {
        if (scenario.Expected.Oracles.Count == 0)
        {
            return
            [
                new OracleResult
                {
                    OracleName = "ExplicitOracles",
                    Passed = false,
                    Message = "Scenario has no explicit oracles.",
                    Expected = "At least one oracle",
                    Actual = "0 oracles",
                }
            ];
        }

        var results = new List<OracleResult>(scenario.Expected.Oracles.Count);
        foreach (var definition in scenario.Expected.Oracles)
        {
            try
            {
                results.Add(oracleFactory.Create(definition).Evaluate(scenario, trace));
            }
            catch (Exception exception)
            {
                results.Add(new OracleResult
                {
                    OracleName = definition.Name ?? definition.Type.ToString(),
                    Passed = false,
                    Message = $"Oracle configuration failed: {exception.Message}",
                });
            }
        }

        return results;
    }
}
