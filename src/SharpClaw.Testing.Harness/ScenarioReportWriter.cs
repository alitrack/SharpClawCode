using System.Text;
using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Writes markdown reports for scenario suite results.
/// </summary>
public sealed class ScenarioReportWriter
{
    /// <summary>
    /// Writes a markdown report.
    /// </summary>
    /// <param name="result">Suite result to report.</param>
    /// <param name="path">Destination markdown path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteMarkdownAsync(ScenarioSuiteResult result, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Agent Testing Run Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: `{result.GeneratedAtUtc:O}`");
        builder.AppendLine($"Gate status: **{(result.Passed ? "PASS" : "FAIL")}**");
        builder.AppendLine();
        builder.AppendLine("## Gates");
        builder.AppendLine();
        builder.AppendLine("| Gate | Status | Message |");
        builder.AppendLine("|------|--------|---------|");
        foreach (var gate in result.Gates)
        {
            builder.AppendLine($"| {Escape(gate.Name)} | {(gate.Passed ? "PASS" : "FAIL")} | {Escape(gate.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Scenarios");
        builder.AppendLine();
        builder.AppendLine("| Scenario | Risk | Status | Trace |");
        builder.AppendLine("|----------|------|--------|-------|");
        foreach (var run in result.Results)
        {
            var tracePath = string.IsNullOrWhiteSpace(run.TracePath) ? "not written" : NormalizeReportPath(run.TracePath, path);
            builder.AppendLine($"| {Escape(run.Scenario.Id)} | {run.Scenario.Risk} | {(run.Passed ? "PASS" : "FAIL")} | {Escape(tracePath)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Oracle Results");
        foreach (var run in result.Results)
        {
            builder.AppendLine();
            builder.AppendLine($"### {run.Scenario.Id}");
            builder.AppendLine();
            builder.AppendLine($"Final answer: `{run.Trace.FinalAnswer ?? string.Empty}`");
            if (!string.IsNullOrWhiteSpace(run.Trace.ErrorMessage))
            {
                builder.AppendLine($"Executor error: `{run.Trace.ErrorMessage}`");
            }

            builder.AppendLine();
            builder.AppendLine("| Oracle | Status | Message | Expected | Actual |");
            builder.AppendLine("|--------|--------|---------|----------|--------|");
            foreach (var oracle in run.OracleResults)
            {
                builder.AppendLine($"| {Escape(oracle.OracleName)} | {(oracle.Passed ? "PASS" : "FAIL")} | {Escape(oracle.Message)} | {Escape(oracle.Expected ?? string.Empty)} | {Escape(oracle.Actual ?? string.Empty)} |");
            }
        }

        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string NormalizeReportPath(string tracePath, string reportPath)
    {
        if (!Path.IsPathRooted(tracePath))
        {
            return tracePath;
        }

        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (string.IsNullOrWhiteSpace(reportDirectory))
        {
            return tracePath;
        }

        return Path.GetRelativePath(reportDirectory, tracePath);
    }
}
