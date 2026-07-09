# Work Items

Work-item integrations import real tasks into SharpClaw sessions and export session summaries without posting back automatically.

## GitHub

```bash
sharpclaw work import https://github.com/owner/repo/issues/123
sharpclaw work import https://github.com/owner/repo/pull/123
```

Public issues and PRs work unauthenticated. Set `GITHUB_TOKEN` for private repositories or higher rate limits. You can change the token environment variable with `WorkItems:GitHubTokenEnvironmentVariable`.

## Generic JSON

```bash
sharpclaw work import ./task.json --provider generic
```

Expected JSON fields: `provider`, `id`, `title`, `description`, `url`, `status`, `labels`, `assignee`, and `metadata`.

```json
{
  "provider": "generic",
  "id": "task-123",
  "title": "Investigate flaky test",
  "description": "Repro and fix intermittent timeout in CI",
  "url": "https://tracker.example.com/tasks/task-123",
  "status": "open",
  "labels": ["ci", "tests"],
  "assignee": "alice",
  "metadata": { "priority": "high" }
}
```

## Export

```bash
sharpclaw work export-summary --session session_123
```

Export creates Markdown or JSON text in command output. It does not post comments to GitHub or Jira.
