using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Code.Infrastructure;
using SharpClaw.Code.WorkItems.Abstractions;
using SharpClaw.Code.WorkItems.Providers;
using SharpClaw.Code.WorkItems.Services;

namespace SharpClaw.Code.WorkItems;

/// <summary>
/// Registers work-item integration services.
/// </summary>
public static class WorkItemsServiceCollectionExtensions
{
    /// <summary>
    /// Adds work-item providers and session-aware services.
    /// </summary>
    public static IServiceCollection AddSharpClawWorkItems(this IServiceCollection services)
    {
        services.AddSharpClawInfrastructure();
        services.AddSingleton<IWorkItemProvider, GitHubWorkItemProvider>();
        services.AddSingleton<IWorkItemProvider, GenericWorkItemProvider>();
        services.AddSingleton<IWorkItemRegistry, WorkItemRegistry>();
        services.AddSingleton<IWorkItemService, WorkItemService>();
        return services;
    }
}
