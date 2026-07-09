using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Skills.Abstractions;

/// <summary>
/// Manages reusable skill packs.
/// </summary>
public interface ISkillPackRegistry
{
    /// <summary>
    /// Lists built-in, user, and workspace skill packs.
    /// </summary>
    Task<IReadOnlyList<SkillPack>> ListAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a skill pack by id.
    /// </summary>
    Task<SkillPack?> ResolveAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken);

    /// <summary>
    /// Installs a local skill pack manifest or directory into the workspace.
    /// </summary>
    Task<SkillPack> InstallAsync(string workspaceRoot, SkillPackInstallRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Enables a workspace skill pack.
    /// </summary>
    Task<bool> EnableAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken);

    /// <summary>
    /// Disables a workspace skill pack.
    /// </summary>
    Task<bool> DisableAsync(string workspaceRoot, string skillId, CancellationToken cancellationToken);

    /// <summary>
    /// Expands the skill pack entry prompt for execution.
    /// </summary>
    Task<string> BuildPromptAsync(string workspaceRoot, SkillPackRunRequest request, CancellationToken cancellationToken);
}
