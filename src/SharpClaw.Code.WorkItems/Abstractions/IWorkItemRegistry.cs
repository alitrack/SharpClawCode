namespace SharpClaw.Code.WorkItems.Abstractions;

/// <summary>
/// Registry for work-item providers.
/// </summary>
public interface IWorkItemRegistry
{
    /// <summary>
    /// Resolves a provider for an import request.
    /// </summary>
    IWorkItemProvider? Resolve(string provider, string idOrUrl);
}
