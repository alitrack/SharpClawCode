using SharpClaw.Code.ExternalAgents.Abstractions;

namespace SharpClaw.Code.ExternalAgents.Services;

/// <summary>
/// Resolves external agent executables using absolute paths and PATH lookup.
/// </summary>
public sealed class PathExternalAgentExecutableResolver : IExternalAgentExecutableResolver
{
    /// <inheritdoc />
    public string? Resolve(string executableNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(executableNameOrPath))
        {
            return null;
        }

        if (Path.IsPathFullyQualified(executableNameOrPath) || executableNameOrPath.Contains(Path.DirectorySeparatorChar) || executableNameOrPath.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(executableNameOrPath) ? executableNameOrPath : null;
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [string.Empty];

        foreach (var directory in paths)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executableNameOrPath + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
