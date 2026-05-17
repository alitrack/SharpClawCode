# Work Items

Work-item integrations import real tasks into SharpClaw sessions and export session summaries without posting back automatically.

## GitHub

```bash
sharpclaw work import https://github.com/owner/repo/issues/123
sharpclaw work import https://github.com/owner/repo/pull/123
```

Public issues and PRs work unauthenticated. Set `GITHUB_TOKEN` for private repositories or higher rate limits.

## Generic JSON

```bash
sharpclaw work import ./task.json --provider generic
```

The JSON shape matches `GenericWorkItemFixture`: provider, id, title, description, url, status, labels, assignee, and metadata.

## Export

```bash
sharpclaw work export-summary --session session_123
```

Export creates Markdown or JSON text in command output. It does not post comments to GitHub or Jira.
