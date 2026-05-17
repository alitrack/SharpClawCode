using System.Text.Json;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Skills.Abstractions;

namespace SharpClaw.Code.Skills.Services;

/// <summary>
/// File-backed skill-pack registry with built-in pack support.
/// </summary>
public sealed class SkillPackRegistry(
    IFileSystem fileSystem,
    IPathService pathService,
    IUserProfilePaths userProfilePaths,
    ISystemClock systemClock) : ISkillPackRegistry
{
    private const string WorkspaceRootDirectoryName = ".sharpclaw";
    private const string SkillPacksDirectoryName = "skillpacks";
    private const string ManifestFileName = "skillpack.json";
    private const string DisabledFileName = ".disabled";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillPack>> ListAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var packs = new Dictionary<string, SkillPack>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in BuiltInPacks())
        {
            packs[pack.Manifest.Id] = pack;
        }

        foreach (var root in GetRoots(workspaceRoot))
        {
            if (!fileSystem.DirectoryExists(root))
            {
                continue;
            }

            foreach (var directory in fileSystem.EnumerateDirectories(root))
            {
                var pack = await ResolveFromDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
                if (pack is not null)
                {
                    packs[pack.Manifest.Id] = pack;
                }
            }
        }

        return packs.Values.OrderBy(pack => pack.Manifest.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <inheritdoc />
    public async Task<SkillPack?> ResolveAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken)
        => (await ListAsync(workspaceRoot, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(pack => string.Equals(pack.Manifest.Id, skillId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pack.Manifest.Name, skillId, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async Task<SkillPack> InstallAsync(string workspaceRoot, SkillPackInstallRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var manifestPath = fileSystem.DirectoryExists(request.SourcePath)
            ? pathService.Combine(pathService.GetFullPath(request.SourcePath), ManifestFileName)
            : pathService.GetFullPath(request.SourcePath);
        var manifestText = await fileSystem.ReadAllTextIfExistsAsync(manifestPath, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Skill pack manifest '{manifestPath}' was not found.");
        var manifest = JsonSerializer.Deserialize(manifestText, ProtocolJsonContext.Default.SkillPackManifest)
            ?? throw new InvalidOperationException($"Skill pack manifest '{manifestPath}' could not be parsed.");
        Validate(manifest);

        var targetDirectory = pathService.Combine(GetWorkspaceRoot(workspaceRoot, ensureExists: true), manifest.Id);
        fileSystem.CreateDirectory(targetDirectory);
        await fileSystem.WriteAllTextAsync(
            pathService.Combine(targetDirectory, ManifestFileName),
            JsonSerializer.Serialize(manifest, ProtocolJsonContext.Default.SkillPackManifest),
            cancellationToken).ConfigureAwait(false);

        if (request.EnableAfterInstall)
        {
            fileSystem.TryDeleteFile(pathService.Combine(targetDirectory, DisabledFileName));
        }
        else
        {
            await fileSystem.WriteAllTextAsync(pathService.Combine(targetDirectory, DisabledFileName), systemClock.UtcNow.ToString("O"), cancellationToken).ConfigureAwait(false);
        }

        return new SkillPack(manifest, targetDirectory, false, request.EnableAfterInstall, systemClock.UtcNow);
    }

    /// <inheritdoc />
    public Task<bool> EnableAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var directory = pathService.Combine(GetWorkspaceRoot(workspaceRoot, ensureExists: false), skillId);
        if (!fileSystem.DirectoryExists(directory))
        {
            return Task.FromResult(false);
        }

        fileSystem.TryDeleteFile(pathService.Combine(directory, DisabledFileName));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<bool> DisableAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken)
    {
        var directory = pathService.Combine(GetWorkspaceRoot(workspaceRoot, ensureExists: false), skillId);
        if (!fileSystem.DirectoryExists(directory))
        {
            return false;
        }

        await fileSystem.WriteAllTextAsync(pathService.Combine(directory, DisabledFileName), systemClock.UtcNow.ToString("O"), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<string> BuildPromptAsync(string workspaceRoot, SkillPackRunRequest request, CancellationToken cancellationToken)
    {
        var pack = await ResolveAsync(workspaceRoot, request.SkillId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Skill pack '{request.SkillId}' was not found.");
        if (!pack.Enabled)
        {
            throw new InvalidOperationException($"Skill pack '{request.SkillId}' is disabled.");
        }

        var template = request.CommandName is null
            ? pack.Manifest.EntryPointPrompt
            : pack.Manifest.Commands?.FirstOrDefault(command => string.Equals(command.Name, request.CommandName, StringComparison.OrdinalIgnoreCase))?.PromptTemplate
                ?? throw new InvalidOperationException($"Skill pack command '{request.CommandName}' was not found.");

        return template
            .Replace("{{arguments}}", request.Arguments ?? string.Empty, StringComparison.Ordinal)
            .Replace("{{workspace}}", pathService.GetFullPath(workspaceRoot), StringComparison.Ordinal);
    }

    private async Task<SkillPack?> ResolveFromDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var manifestText = await fileSystem.ReadAllTextIfExistsAsync(pathService.Combine(directory, ManifestFileName), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(manifestText))
        {
            return null;
        }

        var manifest = JsonSerializer.Deserialize(manifestText, ProtocolJsonContext.Default.SkillPackManifest);
        if (manifest is null)
        {
            return null;
        }

        return new SkillPack(manifest, directory, false, !fileSystem.FileExists(pathService.Combine(directory, DisabledFileName)));
    }

    private IEnumerable<string> GetRoots(string workspaceRoot)
    {
        yield return pathService.Combine(userProfilePaths.GetUserSharpClawRoot(), SkillPacksDirectoryName);
        yield return GetWorkspaceRoot(workspaceRoot, ensureExists: false);
    }

    private string GetWorkspaceRoot(string workspaceRoot, bool ensureExists)
    {
        var normalized = pathService.GetFullPath(workspaceRoot);
        var root = pathService.Combine(normalized, WorkspaceRootDirectoryName, SkillPacksDirectoryName);
        if (ensureExists)
        {
            fileSystem.CreateDirectory(root);
        }

        return root;
    }

    private static void Validate(SkillPackManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.EntryPointPrompt);
    }

    private static IReadOnlyList<SkillPack> BuiltInPacks()
        =>
        [
            BuiltIn("pr-review", "PR Review Skill", "Review the diff or PR context for correctness, tests, security, and maintainability.", "Review this PR or diff. Use this checklist: correctness, tests, security, maintainability. User context: {{arguments}}"),
            BuiltIn("architecture-review", "Architecture Review Skill", "Review boundaries, dependencies, state, and observability.", "Review the architecture for boundaries, dependencies, state handling, and observability. User context: {{arguments}}"),
            BuiltIn("release-notes", "Release Notes Skill", "Summarize commits or session events into release notes.", "Create release notes from the supplied commits, session events, or context. User context: {{arguments}}"),
            BuiltIn("efh-recovery", "EFH Recovery Skill", "Reorient from checkpoints, unresolved intentions, and goal state.", "Recover context from checkpoints, unresolved intentions, and the current goal state. User context: {{arguments}}"),
            BuiltIn("figma-handoff", "Figma Handoff Skill", "Convert UI or design notes into implementation tasks.", "Convert these Figma/design notes into concrete implementation tasks. User context: {{arguments}}"),
        ];

    private static SkillPack BuiltIn(string id, string name, string description, string prompt)
        => new(
            new SkillPackManifest(
                id,
                name,
                "1.0.0",
                description,
                "SharpClaw",
                ["built-in"],
                [new SkillCommand("run", "Runs the skill entrypoint.", prompt)],
                [new SkillPromptTemplate("entrypoint", description, prompt)],
                [new SkillChecklist("default", ["Correctness", "Tests", "Security", "Maintainability"])],
                null,
                [ApprovalScope.ToolExecution],
                [PrimaryMode.Plan, PrimaryMode.Build, PrimaryMode.Research],
                prompt),
            "built-in",
            true,
            true);
}
