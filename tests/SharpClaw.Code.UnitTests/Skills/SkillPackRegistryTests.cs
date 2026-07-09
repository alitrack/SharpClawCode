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
            Id: "custom-review",
            Name: "Custom Review",
            Version: "1.0.0",
            Description: "Custom review pack",
            Author: "tests",
            Tags: ["review"],
            Commands: null,
            Prompts: null,
            Checklists: null,
            RecommendedTools: null,
            RequiredPermissions: null,
            CompatibleModes: null,
            EntryPointPrompt: "Review {{arguments}} in {{workspace}}.");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ProtocolJsonContext.Default.SkillPackManifest));
        var registry = CreateRegistry();

        var installed = await registry.InstallAsync(workspace, new SkillPackInstallRequest(manifestPath), CancellationToken.None);
        var prompt = await registry.BuildPromptAsync(workspace, new SkillPackRunRequest(installed.Manifest.Id, "abc"), CancellationToken.None);

        installed.Manifest.Id.Should().Be("custom-review");
        prompt.Should().Contain("abc").And.Contain(workspace);
    }

    [Fact]
    public async Task InstallAsync_should_reject_unsafe_manifest_id()
    {
        Directory.CreateDirectory(workspace);
        var manifestPath = Path.Combine(workspace, "skillpack.json");
        var manifest = new SkillPackManifest(
            Id: "../escape",
            Name: "Bad Pack",
            Version: "1.0.0",
            Description: "Bad pack",
            Author: "tests",
            Tags: null,
            Commands: null,
            Prompts: null,
            Checklists: null,
            RecommendedTools: null,
            RequiredPermissions: null,
            CompatibleModes: null,
            EntryPointPrompt: "Run.");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ProtocolJsonContext.Default.SkillPackManifest));
        var registry = CreateRegistry();

        var act = () => registry.InstallAsync(workspace, new SkillPackInstallRequest(manifestPath), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*safe single directory name*");
    }

    [Fact]
    public async Task ListAsync_should_skip_malformed_skill_pack_manifest()
    {
        var badPackDirectory = Path.Combine(workspace, ".sharpclaw", "skillpacks", "bad");
        Directory.CreateDirectory(badPackDirectory);
        await File.WriteAllTextAsync(Path.Combine(badPackDirectory, "skillpack.json"), "{ not-json");
        var registry = CreateRegistry();

        var packs = await registry.ListAsync(workspace, CancellationToken.None);

        packs.Should().NotContain(pack => pack.Source == badPackDirectory);
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
