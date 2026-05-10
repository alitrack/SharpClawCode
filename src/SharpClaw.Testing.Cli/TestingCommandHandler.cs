using System.CommandLine;
using System.Text.Json;
using SharpClaw.Code.Commands;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Testing.Abstractions;
using SharpClaw.Testing.Harness;

namespace SharpClaw.Testing.Cli;

/// <summary>
/// Implements the <c>sharpclaw test</c> command family.
/// </summary>
public sealed class TestingCommandHandler(
    ScenarioReportWriter reportWriter,
    ScenarioResultStore resultStore) : ICommandHandler
{
    private const string DefaultScenarioDirectory = "tests/agent-scenarios";
    private const string DefaultReportPath = "docs/testing/test-run-report.md";
    private const string DefaultResultPath = "artifacts/testing/test-run-results.json";
    private const string DefaultTraceDirectory = "artifacts/testing/traces";

    /// <inheritdoc />
    public string Name => "test";

    /// <inheritdoc />
    public string Description => "Runs explicit scenario-based agent harness tests.";

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        command.Subcommands.Add(BuildInitCommand(globalOptions));
        command.Subcommands.Add(BuildRunCommand(globalOptions));
        command.Subcommands.Add(BuildReportCommand(globalOptions));
        command.Subcommands.Add(BuildGatesCommand(globalOptions));
        return command;
    }

    private Command BuildInitCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("init", "Creates the default agent scenario directory and example scenarios.");
        var scenariosOption = ScenarioDirectoryOption();
        var forceOption = new Option<bool>("--force") { Description = "Overwrite existing example scenario files." };
        command.Options.Add(scenariosOption);
        command.Options.Add(forceOption);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var context = globalOptions.Resolve(parseResult);
            return InitializeAsync(
                context,
                parseResult.GetValue(scenariosOption),
                parseResult.GetValue(forceOption),
                cancellationToken);
        });
        return command;
    }

    private Command BuildRunCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("run", "Runs discovered scenarios, writes traces, evaluates oracles, and emits a report.");
        var scenariosOption = ScenarioDirectoryOption();
        var reportOption = ReportPathOption();
        var resultsOption = ResultsPathOption();
        var traceOption = TraceDirectoryOption();
        AddCommonRunOptions(command, scenariosOption, reportOption, resultsOption, traceOption);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var context = globalOptions.Resolve(parseResult);
            return RunAndPersistAsync(
                context,
                parseResult.GetValue(scenariosOption),
                parseResult.GetValue(reportOption),
                parseResult.GetValue(resultsOption),
                parseResult.GetValue(traceOption),
                printGatesOnly: false,
                cancellationToken);
        });
        return command;
    }

    private Command BuildReportCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("report", "Writes a markdown report from the latest result file, or runs scenarios if no result file exists.");
        var scenariosOption = ScenarioDirectoryOption();
        var reportOption = ReportPathOption();
        var resultsOption = ResultsPathOption();
        var traceOption = TraceDirectoryOption();
        AddCommonRunOptions(command, scenariosOption, reportOption, resultsOption, traceOption);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var context = globalOptions.Resolve(parseResult);
            return ReportAsync(
                context,
                parseResult.GetValue(scenariosOption),
                parseResult.GetValue(reportOption),
                parseResult.GetValue(resultsOption),
                parseResult.GetValue(traceOption),
                cancellationToken);
        });
        return command;
    }

    private Command BuildGatesCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command("gates", "Runs scenarios and returns non-zero when quality gates fail.");
        var scenariosOption = ScenarioDirectoryOption();
        var reportOption = ReportPathOption();
        var resultsOption = ResultsPathOption();
        var traceOption = TraceDirectoryOption();
        AddCommonRunOptions(command, scenariosOption, reportOption, resultsOption, traceOption);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var context = globalOptions.Resolve(parseResult);
            return RunAndPersistAsync(
                context,
                parseResult.GetValue(scenariosOption),
                parseResult.GetValue(reportOption),
                parseResult.GetValue(resultsOption),
                parseResult.GetValue(traceOption),
                printGatesOnly: true,
                cancellationToken);
        });
        return command;
    }

    private async Task<int> InitializeAsync(
        CommandExecutionContext context,
        string? scenarioDirectory,
        bool force,
        CancellationToken cancellationToken)
    {
        var scenarioRoot = ResolvePath(context.WorkingDirectory, scenarioDirectory, DefaultScenarioDirectory);
        Directory.CreateDirectory(scenarioRoot);
        Directory.CreateDirectory(ResolvePath(context.WorkingDirectory, null, "docs/testing"));

        var created = 0;
        var skipped = 0;
        foreach (var scenario in ExampleScenarioCatalog.CreateDefaultScenarios())
        {
            var path = Path.Combine(scenarioRoot, $"{scenario.Id}.json");
            if (File.Exists(path) && !force)
            {
                skipped++;
                continue;
            }

            await using var stream = File.Create(path);
            await JsonSerializer
                .SerializeAsync(stream, scenario, ScenarioJsonContext.Default.AgentScenario, cancellationToken)
                .ConfigureAwait(false);
            created++;
        }

        await Console.Out.WriteLineAsync($"Initialized agent scenarios at {scenarioRoot}. Created {created}, skipped {skipped}.").ConfigureAwait(false);
        return 0;
    }

    private async Task<int> RunAndPersistAsync(
        CommandExecutionContext context,
        string? scenarioDirectory,
        string? reportPath,
        string? resultsPath,
        string? traceDirectory,
        bool printGatesOnly,
        CancellationToken cancellationToken)
    {
        var scenarios = ResolvePath(context.WorkingDirectory, scenarioDirectory, DefaultScenarioDirectory);
        var report = ResolvePath(context.WorkingDirectory, reportPath, DefaultReportPath);
        var results = ResolvePath(context.WorkingDirectory, resultsPath, DefaultResultPath);
        var traces = ResolvePath(context.WorkingDirectory, traceDirectory, DefaultTraceDirectory);
        var suite = await ScenarioSuiteRunner.CreateDefault(traces)
            .RunDirectoryAsync(scenarios, cancellationToken)
            .ConfigureAwait(false);

        await resultStore.WriteAsync(suite, results, cancellationToken).ConfigureAwait(false);
        await reportWriter.WriteMarkdownAsync(suite, report, cancellationToken).ConfigureAwait(false);
        await PrintSummaryAsync(suite, report, results, printGatesOnly).ConfigureAwait(false);
        return suite.Passed ? 0 : 1;
    }

    private async Task<int> ReportAsync(
        CommandExecutionContext context,
        string? scenarioDirectory,
        string? reportPath,
        string? resultsPath,
        string? traceDirectory,
        CancellationToken cancellationToken)
    {
        var report = ResolvePath(context.WorkingDirectory, reportPath, DefaultReportPath);
        var results = ResolvePath(context.WorkingDirectory, resultsPath, DefaultResultPath);
        ScenarioSuiteResult suite;

        if (File.Exists(results))
        {
            suite = await resultStore.ReadAsync(results, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var scenarios = ResolvePath(context.WorkingDirectory, scenarioDirectory, DefaultScenarioDirectory);
            var traces = ResolvePath(context.WorkingDirectory, traceDirectory, DefaultTraceDirectory);
            suite = await ScenarioSuiteRunner.CreateDefault(traces)
                .RunDirectoryAsync(scenarios, cancellationToken)
                .ConfigureAwait(false);
            await resultStore.WriteAsync(suite, results, cancellationToken).ConfigureAwait(false);
        }

        await reportWriter.WriteMarkdownAsync(suite, report, cancellationToken).ConfigureAwait(false);
        await PrintSummaryAsync(suite, report, results, printGatesOnly: false).ConfigureAwait(false);
        return suite.Passed ? 0 : 1;
    }

    private static async Task PrintSummaryAsync(
        ScenarioSuiteResult suite,
        string reportPath,
        string resultsPath,
        bool printGatesOnly)
    {
        if (!printGatesOnly)
        {
            var passed = suite.Results.Count(static result => result.Passed);
            await Console.Out.WriteLineAsync($"Scenarios: {passed}/{suite.Results.Count} passed.").ConfigureAwait(false);
        }

        foreach (var gate in suite.Gates)
        {
            await Console.Out.WriteLineAsync($"Gate {gate.Name}: {(gate.Passed ? "PASS" : "FAIL")} - {gate.Message}").ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync($"Report: {reportPath}").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Results: {resultsPath}").ConfigureAwait(false);
    }

    private static void AddCommonRunOptions(
        Command command,
        Option<string?> scenariosOption,
        Option<string?> reportOption,
        Option<string?> resultsOption,
        Option<string?> traceOption)
    {
        command.Options.Add(scenariosOption);
        command.Options.Add(reportOption);
        command.Options.Add(resultsOption);
        command.Options.Add(traceOption);
    }

    private static Option<string?> ScenarioDirectoryOption()
        => new("--scenarios")
        {
            Description = $"Scenario directory. Defaults to {DefaultScenarioDirectory}.",
        };

    private static Option<string?> ReportPathOption()
        => new("--report")
        {
            Description = $"Markdown report path. Defaults to {DefaultReportPath}.",
        };

    private static Option<string?> ResultsPathOption()
        => new("--results")
        {
            Description = $"Machine-readable result path. Defaults to {DefaultResultPath}.",
        };

    private static Option<string?> TraceDirectoryOption()
        => new("--trace-dir")
        {
            Description = $"Trace output directory. Defaults to {DefaultTraceDirectory}.",
        };

    private static string ResolvePath(string workingDirectory, string? value, string defaultRelativePath)
    {
        var path = string.IsNullOrWhiteSpace(value) ? defaultRelativePath : value;
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workingDirectory, path));
    }
}
