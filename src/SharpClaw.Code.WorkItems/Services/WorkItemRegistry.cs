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
    {
        var direct = orderedProviders.FirstOrDefault(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct;
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return orderedProviders.FirstOrDefault(item => item.CanImport(idOrUrl));
    }
}
