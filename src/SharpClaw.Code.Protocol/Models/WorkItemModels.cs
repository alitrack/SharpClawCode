using System.Text.Json;

namespace SharpClaw.Code.Protocol.Models;

/// <summary>
/// Imported work-item source kind.
/// </summary>
public enum WorkItemProviderKind
{
    /// <summary>GitHub issue or pull request.</summary>
    GitHub,

    /// <summary>Generic Jira-style/manual JSON work item.</summary>
    Generic,
}

/// <summary>
/// Normalized work item.
/// </summary>
public sealed record WorkItem(
    string Provider,
    string Id,
    string Title,
    string? Description,
    string? Url,
    string? Status,
    IReadOnlyList<string>? Labels,
    string? Assignee,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>
/// Request to import a work item into a session.
/// </summary>
public sealed record WorkItemImportRequest(
    string Provider,
    string IdOrUrl,
    string WorkspacePath,
    string? Mode = null,
    string? SessionId = null);

/// <summary>
/// Request to export a work-item-aware session summary.
/// </summary>
public sealed record WorkItemExportRequest(
    string Provider,
    string? SessionId,
    string? TargetIdOrUrl,
    string ExportFormat = "markdown");

/// <summary>
/// Imported work item plus linked session id.
/// </summary>
public sealed record WorkItemImportResult(
    WorkItem WorkItem,
    string SessionId,
    bool CreatedSession);

/// <summary>
/// Work-item summary export result.
/// </summary>
public sealed record WorkItemSummaryExport(
    string SessionId,
    string Format,
    string Content,
    WorkItem? WorkItem);

/// <summary>
/// GitHub issue or pull request URL parse result.
/// </summary>
public sealed record GitHubWorkItemReference(
    string Owner,
    string Repository,
    string Kind,
    int Number,
    string Url);

/// <summary>
/// Configures work-item integrations.
/// </summary>
public sealed record WorkItemsConfig(
    bool Enabled = true,
    string? GitHubTokenEnvironmentVariable = "GITHUB_TOKEN");

/// <summary>
/// JSON fixture wrapper for generic work item ingestion.
/// </summary>
public sealed record GenericWorkItemFixture(
    string Provider,
    string Id,
    string Title,
    string? Description,
    string? Url,
    string? Status,
    string[]? Labels,
    string? Assignee,
    Dictionary<string, string>? Metadata)
{
    /// <summary>
    /// Converts the fixture to a normalized work item.
    /// </summary>
    public WorkItem ToWorkItem()
        => new(
            Provider,
            Id,
            Title,
            Description,
            Url,
            Status,
            Labels,
            Assignee,
            Metadata);
}
