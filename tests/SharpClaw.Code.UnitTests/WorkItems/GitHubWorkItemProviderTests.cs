using FluentAssertions;
using SharpClaw.Code.WorkItems.Providers;

namespace SharpClaw.Code.UnitTests.WorkItems;

public sealed class GitHubWorkItemProviderTests
{
    [Theory]
    [InlineData("https://github.com/clawdotnet/SharpClawCode/issues/123", "issues", 123)]
    [InlineData("https://github.com/clawdotnet/SharpClawCode/pull/45", "pull", 45)]
    public void TryParse_should_parse_issue_and_pr_urls(string url, string kind, int number)
    {
        var parsed = GitHubWorkItemProvider.TryParse(url, out var reference);

        parsed.Should().BeTrue();
        reference.Owner.Should().Be("clawdotnet");
        reference.Repository.Should().Be("SharpClawCode");
        reference.Kind.Should().Be(kind);
        reference.Number.Should().Be(number);
    }
}
