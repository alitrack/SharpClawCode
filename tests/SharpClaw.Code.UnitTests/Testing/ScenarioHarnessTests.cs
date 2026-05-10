using SharpClaw.Testing.Abstractions;
using SharpClaw.Testing.Harness;
using SharpClaw.Testing.Xunit;

namespace SharpClaw.Code.UnitTests.Testing;

public sealed class ScenarioHarnessTests
{
    public static IEnumerable<object[]> ExampleScenarios
        => XunitScenarioData.LoadDirectory(Path.Combine(RepositoryRoot(), "tests", "agent-scenarios"));

    [Theory]
    [MemberData(nameof(ExampleScenarios))]
    public Task Example_scenario_passes_under_xunit_adapter(AgentScenario scenario)
        => XunitScenarioAssert.PassesAsync(scenario);

    [Fact]
    public async Task Failed_oracle_result_reports_actionable_message()
    {
        var scenario = new AgentScenario
        {
            Id = "missing-tool",
            Name = "Missing tool",
            Risk = ScenarioRisk.High,
            Input = new ScenarioInput
            {
                Prompt = "Do not call a tool.",
                ScriptedFinalAnswer = "No tool was needed.",
            },
            Expected = new ScenarioExpected
            {
                Oracles =
                [
                    new ScenarioOracleDefinition
                    {
                        Type = ScenarioOracleType.ToolCalled,
                        ToolName = "read_file",
                    },
                ],
            },
        };

        var result = await ScenarioRunner.CreateDefault().RunAsync(scenario, CancellationToken.None);

        Assert.False(result.Passed);
        var oracle = Assert.Single(result.OracleResults);
        Assert.Contains("was not called", oracle.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("read_file", oracle.Expected);
    }

    [Fact]
    public async Task Suite_gates_fail_when_high_risk_scenario_fails()
    {
        var scenario = new AgentScenario
        {
            Id = "high-risk-failure",
            Name = "High risk failure",
            Risk = ScenarioRisk.High,
            Input = new ScenarioInput
            {
                Prompt = "Return the wrong answer.",
                ScriptedFinalAnswer = "wrong",
            },
            Expected = new ScenarioExpected
            {
                Oracles =
                [
                    new ScenarioOracleDefinition
                    {
                        Type = ScenarioOracleType.FinalAnswerContains,
                        Text = "expected",
                    },
                ],
            },
        };

        var run = await ScenarioRunner.CreateDefault().RunAsync(scenario, CancellationToken.None);
        var gates = new ScenarioGateEvaluator().Evaluate([run]);

        Assert.False(gates.Single(gate => gate.Name == "high-risk-pass").Passed);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharpClawCode.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate SharpClawCode.sln from the test output directory.");
    }
}
