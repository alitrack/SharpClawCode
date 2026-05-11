namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Bootstraps minimal SharpClaw workspace files and directories.
/// </summary>
public interface IWorkspaceBootstrapService
{
    /// <summary>
    /// Initializes the workspace SharpClaw layout.
    /// </summary>
    Task<WorkspaceBootstrapResult> InitializeAsync(
        string workspaceRoot,
        bool force,
        bool includeCommandsDirectory,
        bool includeSkillsDirectory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of initializing workspace-local SharpClaw scaffolding.
/// </summary>
public sealed record WorkspaceBootstrapResult(
    string WorkspaceRoot,
    string ConfigPath,
    bool ConfigCreated,
    string[] CreatedDirectories);
