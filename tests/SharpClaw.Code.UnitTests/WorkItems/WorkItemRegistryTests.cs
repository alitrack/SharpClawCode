using FluentAssertions;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.WorkItems.Abstractions;
using SharpClaw.Code.WorkItems.Services;

namespace SharpClaw.Code.UnitTests.WorkItems;

public sealed class WorkItemRegistryTests
{
    [Fact]
    public void Resolve_should_not_fallback_when_explicit_provider_is_unknown()
    {
        var registry = new WorkItemRegistry([new TestProvider("generic", true)]);

        var provider = registry.Resolve("missing", "task.json");

        provider.Should().BeNull();
    }

    [Fact]
    public void Resolve_should_fallback_by_import_capability_when_provider_is_empty()
    {
        var registry = new WorkItemRegistry([new TestProvider("generic", true)]);

        var provider = registry.Resolve(string.Empty, "task.json");

        provider.Should().NotBeNull();
        provider!.Provider.Should().Be("generic");
    }

    private sealed class TestProvider(string provider, bool canImport) : IWorkItemProvider
    {
        public string Provider => provider;

        public bool CanImport(string idOrUrl) => canImport;

        public Task<WorkItem> ImportAsync(WorkItemImportRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
