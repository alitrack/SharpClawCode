using System.Text.Json;
using FluentAssertions;
using SharpClaw.Code.Infrastructure.Services;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Skills.Services;

namespace SharpClaw.Code.UnitTests.Skills;

public sealed class SkillPackRegistryTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), $"sharpclaw-skillpacks-{Guid.NewGuid():N}");

    [Fact]
    public async Task ListAsync_should_include_built_in_skill_packs()
    {
        var registry = CreateRegistry();

        var packs = await registry.ListAsync(workspace, CancellationToken.None);

        packs.Should().Contain(pack => pack.Manifest.Id == "pr-review" && pack.IsBuiltIn);
    }

    [Fact]
    public async Task InstallAsync_should_parse_manifest_and_build_prompt()
    {
        Directory.CreateDirectory(workspace);
        var manifestPath = Path.Combine(workspace, "skillpack.json");
        var manifest = new SkillPackManifest(
            "custom-review",
            "Custom Review",
            "1.0.0",
            "Custom review pack",
            "tests",
            ["review"],
            null,
            null,
            null,
            null,
            null,
            null,
            "Review {{arguments}} in {{workspace}}.");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ProtocolJsonContext.Default.SkillPackManifest));
        var registry = CreateRegistry();

        var installed = await registry.InstallAsync(workspace, new SkillPackInstallRequest(manifestPath), CancellationToken.None);
        var prompt = await registry.BuildPromptAsync(workspace, new SkillPackRunRequest(installed.Manifest.Id, "abc"), CancellationToken.None);

        installed.Manifest.Id.Should().Be("custom-review");
        prompt.Should().Contain("abc").And.Contain(workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static SkillPackRegistry CreateRegistry()
    {
        var pathService = new PathService();
        return new SkillPackRegistry(new LocalFileSystem(), pathService, new UserProfilePaths(pathService), new SystemClock());
    }
}
