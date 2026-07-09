using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
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
        services.AddHttpClient(
            GitHubWorkItemProvider.HttpClientName,
            client =>
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SharpClawCode", "1.0"));
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        services.AddSingleton<IWorkItemConfigProvider, DefaultWorkItemConfigProvider>();
        services.AddSingleton<IWorkItemProvider, GitHubWorkItemProvider>();
        services.AddSingleton<IWorkItemProvider, GenericWorkItemProvider>();
        services.AddSingleton<IWorkItemRegistry, WorkItemRegistry>();
        services.AddSingleton<IWorkItemService, WorkItemService>();
        return services;
    }
}
