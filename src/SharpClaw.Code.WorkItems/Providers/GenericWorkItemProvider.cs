using System.Text.Json;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.WorkItems.Providers;

/// <summary>
/// Imports generic Jira-style work items from local JSON.
/// </summary>
public sealed class GenericWorkItemProvider(IFileSystem fileSystem, IPathService pathService) : IWorkItemProvider
{
    /// <inheritdoc />
    public string Provider => "generic";

    /// <inheritdoc />
    public bool CanImport(string idOrUrl) => idOrUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || idOrUrl.TrimStart().StartsWith('{');

    /// <inheritdoc />
    public async Task<WorkItem> ImportAsync(WorkItemImportRequest request, CancellationToken cancellationToken)
    {
        var json = request.IdOrUrl.TrimStart().StartsWith('{')
            ? request.IdOrUrl
            : await fileSystem.ReadAllTextIfExistsAsync(pathService.GetFullPath(request.IdOrUrl), cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Generic work-item fixture '{request.IdOrUrl}' was not found.");
        var fixture = JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.GenericWorkItemFixture)
            ?? throw new InvalidOperationException("Generic work-item fixture could not be parsed.");
        return fixture.ToWorkItem();
    }
}
