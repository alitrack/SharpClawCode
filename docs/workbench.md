# Workbench

The workbench is a static terminal status surface focused on SharpClaw runtime state. It is not a terminal emulator or live dashboard.

```bash
sharpclaw workbench
```

REPL commands:

```text
/workbench
/sessions
/approvals
/checkpoints
/agent-status
```

The report includes the current session, goal, primary mode, active agent, approval summary, latest checkpoint, recent runtime activity, external adapter health, and warnings from operational status checks.

Use `--output-format json` for stable machine-readable output.
