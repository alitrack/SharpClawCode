# Agent Testing Harness

## Purpose

The agent testing harness is a disciplined scenario runner for SharpClaw agent behavior. It is not a generic AI test generator. Each scenario declares the prompt, the trace source, risk level, and explicit oracles that must pass.

Every run produces a structured trace and evaluates that trace against named oracles. The first implementation uses a `scripted` executor so the model, loader, trace writer, report writer, gates, CLI, and xUnit adapter can stabilize before wiring the harness to the live runtime/gateway.

## Scenario Format

Scenarios live under `tests/agent-scenarios` as JSON files:

```json
{
  "id": "basic-tool-call",
  "risk": "Low",
  "input": {
    "prompt": "Read the project README.",
    "executor": "scripted",
    "scriptedTrace": [
      {
        "kind": "ToolCall",
        "toolCall": {
          "toolName": "read_file",
          "argumentsJson": "{\"path\":\"README.md\"}"
        }
      }
    ],
    "scriptedFinalAnswer": "README starts with SharpClaw Code."
  },
  "expected": {
    "oracles": [
      { "type": "ToolCalled", "toolName": "read_file" },
      { "type": "FinalAnswerContains", "text": "SharpClaw Code" }
    ]
  }
}
```

The JSON contracts are defined in `SharpClaw.Testing.Abstractions` and serialized with `System.Text.Json`. The shape avoids runtime reflection-heavy polymorphic JSON: `TraceStep` has explicit optional payloads such as `toolCall`, `toolResult`, and `stateChange`.

## Oracle Model

Built-in oracles:

- `ToolCalled`
- `ToolNotCalled`
- `FinalAnswerContains`
- `MaxToolCalls`
- `StateEquals`
- `ApprovalRequired`
- `NoUnsafeTool`

Failed oracles include a clear message plus expected and actual summaries. Scenarios with no explicit oracles fail the explicit-oracle gate.

## CLI Usage

Initialize example scenarios:

```bash
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj -- test init
```

Run scenarios, write traces, evaluate oracles, and generate `docs/testing/test-run-report.md`:

```bash
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj -- test run
```

Regenerate a markdown report from the latest result file:

```bash
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj -- test report
```

Run gate checks:

```bash
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj -- test gates
```

Defaults:

- Scenarios: `tests/agent-scenarios`
- Markdown report: `docs/testing/test-run-report.md`
- Machine-readable results: `artifacts/testing/test-run-results.json`
- Trace files: `artifacts/testing/traces`

## xUnit Usage

`SharpClaw.Testing.Xunit` exposes data and assertion helpers:

```csharp
public static IEnumerable<object[]> Scenarios
    => XunitScenarioData.LoadDirectory("tests/agent-scenarios");

[Theory]
[MemberData(nameof(Scenarios))]
public Task Scenario_passes(AgentScenario scenario)
    => XunitScenarioAssert.PassesAsync(scenario);
```

The adapter uses explicit `MemberData`; it does not scan assemblies for tests.

## CI Integration

Recommended CI commands:

```bash
dotnet test SharpClawCode.sln --configuration Release
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj --configuration Release --no-build -- test run
dotnet run --project src/SharpClaw.Code.Cli/SharpClaw.Code.Cli.csproj --configuration Release --no-build -- test gates
```

`test run` and `test gates` return non-zero when gates fail. Gates currently require scenario discovery, explicit oracles, passing high/critical risk scenarios, passing scenarios marked `requiredForGates`, and non-empty traces.

## Avoiding Shallow AI-Generated Tests

Generated or hand-authored scenarios are not accepted just because they execute. They must include explicit oracles tied to observable trace behavior, final answers, state transitions, approvals, and tool safety. A scenario with no oracle fails. A high-risk scenario with a failed oracle fails the gate.

Before accepting AI-assisted scenarios, review:

- whether the prompt maps to a real product invariant,
- whether every expected outcome is represented by an oracle,
- whether the trace captures enough evidence for replay,
- whether safety-sensitive tool behavior is checked explicitly,
- whether the risk level is accurate.

## Future Extension Points

The first executor is `scripted`, which acts as a replay foundation. Future runtime integration should add an executor that adapts the real SharpClaw runtime/gateway and emits the same `AgentRunTrace` model.

Likely extensions:

- runtime-backed scenario executor,
- trace replay from captured production traces,
- richer approval and permission trace payloads,
- scenario filters by tag or risk,
- golden trace comparison,
- additional oracles for sessions, provider retries, MCP/plugin lifecycle, and telemetry.
