using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.ExternalAgents.Adapters;
using SharpClaw.Code.ExternalAgents.Services;
using SharpClaw.Code.Infrastructure;

namespace SharpClaw.Code.ExternalAgents;

/// <summary>
/// Registers external agent adapter services.
/// </summary>
public static class ExternalAgentsServiceCollectionExtensions
{
    /// <summary>
    /// Adds SharpClaw external agent adapters.
    /// </summary>
    public static IServiceCollection AddSharpClawExternalAgents(this IServiceCollection services)
    {
        services.AddSharpClawInfrastructure();
        services.AddSingleton<IExternalAgentExecutableResolver, PathExternalAgentExecutableResolver>();
        services.AddSingleton<IExternalAgentConfigProvider, DefaultExternalAgentConfigProvider>();
        services.AddSingleton<IExternalAgentAdapter, ClaudeCodeAdapter>();
        services.AddSingleton<IExternalAgentAdapter, OpenCodeAdapter>();
        services.AddSingleton<IExternalAgentAdapter, GeminiCliAdapter>();
        services.AddSingleton<IExternalAgentAdapter, CodexCliAdapter>();
        services.AddSingleton<IExternalAgentRegistry, ExternalAgentRegistry>();
        services.AddSingleton<IExternalAgentService, ExternalAgentService>();
        return services;
    }
}
