using System.Text;
using SharpClaw.Testing.Abstractions;
using SharpClaw.Testing.Harness;
using Xunit.Sdk;

namespace SharpClaw.Testing.Xunit;

/// <summary>
/// Assertion helpers that run scenarios through the harness under xUnit.
/// </summary>
public static class XunitScenarioAssert
{
    /// <summary>
    /// Runs a scenario and fails the xUnit test when the scenario or any oracle fails.
    /// </summary>
    /// <param name="scenario">Scenario to execute.</param>
    /// <param name="runner">Optional custom runner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task PassesAsync(
        AgentScenario scenario,
        IScenarioRunner? runner = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var effectiveRunner = runner ?? ScenarioRunner.CreateDefault();
        var result = await effectiveRunner.RunAsync(scenario, cancellationToken).ConfigureAwait(false);
        if (result.Passed)
        {
            return;
        }

        throw new XunitException(FormatFailure(result));
    }

    private static string FormatFailure(ScenarioRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Scenario '{result.Scenario.Id}' failed.");
        if (!string.IsNullOrWhiteSpace(result.Trace.ErrorMessage))
        {
            builder.AppendLine($"Executor error: {result.Trace.ErrorMessage}");
        }

        foreach (var oracle in result.OracleResults.Where(static oracle => !oracle.Passed))
        {
            builder.AppendLine($"- {oracle.OracleName}: {oracle.Message}");
            if (!string.IsNullOrWhiteSpace(oracle.Expected) || !string.IsNullOrWhiteSpace(oracle.Actual))
            {
                builder.AppendLine($"  Expected: {oracle.Expected ?? string.Empty}");
                builder.AppendLine($"  Actual: {oracle.Actual ?? string.Empty}");
            }
        }

        return builder.ToString();
    }
}
