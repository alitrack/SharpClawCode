using SharpClaw.Code.WorkItems.Abstractions;

namespace SharpClaw.Code.WorkItems.Services;

/// <summary>
/// Default work-item provider registry.
/// </summary>
public sealed class WorkItemRegistry(IEnumerable<IWorkItemProvider> providers) : IWorkItemRegistry
{
    private readonly IWorkItemProvider[] orderedProviders = providers.ToArray();

    /// <inheritdoc />
    public IWorkItemProvider? Resolve(string provider, string idOrUrl)
        => orderedProviders.FirstOrDefault(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase))
            ?? orderedProviders.FirstOrDefault(item => item.CanImport(idOrUrl));
}
