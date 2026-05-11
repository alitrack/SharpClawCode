using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <inheritdoc />
public sealed class WorkspaceBootstrapService(
    IFileSystem fileSystem,
    IPathService pathService) : IWorkspaceBootstrapService
{
    private const string DefaultConfig = """
{
  // Workspace-local SharpClaw configuration.
  "shareMode": "Manual",
  "server": {
    "host": "127.0.0.1",
    "port": 7345
  }
}
""";

    /// <inheritdoc />
    public async Task<WorkspaceBootstrapResult> InitializeAsync(
        string workspaceRoot,
        bool force,
        bool includeCommandsDirectory,
        bool includeSkillsDirectory,
        CancellationToken cancellationToken)
    {
        var normalized = pathService.GetFullPath(workspaceRoot);
        var sharpClawRoot = pathService.Combine(normalized, ".sharpclaw");
        var configPath = pathService.Combine(sharpClawRoot, "config.jsonc");
        var createdDirectories = new List<string>();
        var configCreated = force || !fileSystem.FileExists(configPath);

        var hadSharpClawRoot = fileSystem.DirectoryExists(sharpClawRoot);
        fileSystem.CreateDirectory(sharpClawRoot);
        if (!hadSharpClawRoot)
        {
            createdDirectories.Add(sharpClawRoot);
        }

        if (configCreated)
        {
            await fileSystem.WriteAllTextAsync(configPath, DefaultConfig, cancellationToken).ConfigureAwait(false);
        }

        if (includeCommandsDirectory)
        {
            var commandsPath = pathService.Combine(sharpClawRoot, "commands");
            var existed = fileSystem.DirectoryExists(commandsPath);
            fileSystem.CreateDirectory(commandsPath);
            if (!existed)
            {
                createdDirectories.Add(commandsPath);
            }
        }

        if (includeSkillsDirectory)
        {
            var skillsPath = pathService.Combine(sharpClawRoot, "skills");
            var existed = fileSystem.DirectoryExists(skillsPath);
            fileSystem.CreateDirectory(skillsPath);
            if (!existed)
            {
                createdDirectories.Add(skillsPath);
            }
        }

        return new WorkspaceBootstrapResult(
            normalized,
            configPath,
            configCreated,
            createdDirectories.ToArray());
    }
}
