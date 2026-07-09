namespace SharpClaw.Code.ExternalAgents.Abstractions;

/// <summary>
/// Resolves an executable path from configuration or PATH.
/// </summary>
public interface IExternalAgentExecutableResolver
{
    /// <summary>
    /// Resolves the executable, returning null when it is unavailable.
    /// </summary>
    string? Resolve(string executableNameOrPath);
}
