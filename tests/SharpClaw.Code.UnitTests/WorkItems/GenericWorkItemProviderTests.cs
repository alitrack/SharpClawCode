using FluentAssertions;
using SharpClaw.Code.Infrastructure.Services;
using SharpClaw.Code.WorkItems.Providers;

namespace SharpClaw.Code.UnitTests.WorkItems;

public sealed class GenericWorkItemProviderTests
{
    [Fact]
    public void CanImport_should_not_claim_missing_json_file()
    {
        var provider = new GenericWorkItemProvider(new LocalFileSystem(), new PathService());

        provider.CanImport(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")).Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_should_wrap_malformed_json()
    {
        var provider = new GenericWorkItemProvider(new LocalFileSystem(), new PathService());

        var act = () => provider.ImportAsync(
            new("generic", "{ not-json", Path.GetTempPath()),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("Generic work-item fixture could not be parsed.");
        exception.Which.InnerException.Should().BeOfType<System.Text.Json.JsonException>();
    }
}
