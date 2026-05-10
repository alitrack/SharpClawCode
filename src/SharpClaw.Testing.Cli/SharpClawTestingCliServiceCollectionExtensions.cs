using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Code.Commands;
using SharpClaw.Testing.Harness;

namespace SharpClaw.Testing.Cli;

/// <summary>
/// Registers the SharpClaw testing CLI command surface.
/// </summary>
public static class SharpClawTestingCliServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <c>test</c> command handler and supporting harness services.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpClawTestingCli(this IServiceCollection services)
    {
        services.AddSharpClawTestingHarness();
        services.AddSingleton<TestingCommandHandler>();
        services.AddSingleton<ICommandHandler>(static serviceProvider => serviceProvider.GetRequiredService<TestingCommandHandler>());
        return services;
    }
}
