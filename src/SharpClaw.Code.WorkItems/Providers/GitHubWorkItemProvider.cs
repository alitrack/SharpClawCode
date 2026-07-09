using System.Net.Http.Headers;
using System.Text.Json;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.WorkItems.Providers;

/// <summary>
/// Imports GitHub issues and pull requests from public REST endpoints or a local token.
/// </summary>
public sealed class GitHubWorkItemProvider(
    IHttpClientFactory httpClientFactory,
    IWorkItemConfigProvider configProvider) : IWorkItemProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Named HTTP client used for GitHub work-item imports.
    /// </summary>
    public const string HttpClientName = "SharpClaw.GitHubWorkItems";

    /// <inheritdoc />
    public string Provider => "github";

    /// <inheritdoc />
    public bool CanImport(string idOrUrl) => TryParse(idOrUrl, out _);

    /// <inheritdoc />
    public async Task<WorkItem> ImportAsync(WorkItemImportRequest request, CancellationToken cancellationToken)
    {
        if (!TryParse(request.IdOrUrl, out var reference))
        {
            throw new InvalidOperationException($"'{request.IdOrUrl}' is not a supported GitHub issue or PR URL.");
        }

        var config = await configProvider.GetConfigAsync(request.WorkspacePath, cancellationToken).ConfigureAwait(false);
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Work-item integrations are disabled.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var tokenEnvironmentVariable = string.IsNullOrWhiteSpace(config.GitHubTokenEnvironmentVariable)
            ? "GITHUB_TOKEN"
            : config.GitHubTokenEnvironmentVariable;
        var token = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{reference.Owner}/{reference.Repository}/{(reference.Kind == "pull" ? "pulls" : "issues")}/{reference.Number}");
        if (!string.IsNullOrWhiteSpace(token))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var labels = root.TryGetProperty("labels", out var labelArray) && labelArray.ValueKind == JsonValueKind.Array
            ? labelArray.EnumerateArray().Select(label => label.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty).Where(static label => !string.IsNullOrWhiteSpace(label)).ToArray()
            : [];
        var assignee = root.TryGetProperty("assignee", out var assigneeElement) && assigneeElement.ValueKind == JsonValueKind.Object && assigneeElement.TryGetProperty("login", out var login)
            ? login.GetString()
            : null;
        return new WorkItem(
            Provider,
            $"{reference.Owner}/{reference.Repository}#{reference.Number}",
            root.GetProperty("title").GetString() ?? reference.Url,
            root.TryGetProperty("body", out var body) ? body.GetString() : null,
            reference.Url,
            root.TryGetProperty("state", out var state) ? state.GetString() : null,
            labels,
            assignee,
            new Dictionary<string, string>
            {
                ["owner"] = reference.Owner,
                ["repository"] = reference.Repository,
                ["kind"] = reference.Kind,
                ["number"] = reference.Number.ToString()
            });
    }

    /// <summary>
    /// Parses GitHub issue and pull request URLs.
    /// </summary>
    public static bool TryParse(string idOrUrl, out GitHubWorkItemReference reference)
    {
        reference = default!;
        if (!Uri.TryCreate(idOrUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || !int.TryParse(parts[3], out var number))
        {
            return false;
        }

        if (!string.Equals(parts[2], "issues", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parts[2], "pull", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        reference = new GitHubWorkItemReference(parts[0], parts[1], parts[2].ToLowerInvariant(), number, idOrUrl);
        return true;
    }
}
