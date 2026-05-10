using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Testing.Abstractions;

namespace SharpClaw.Testing.Harness;

/// <summary>
/// Registers the default scenario testing harness services.
/// </summary>
public static class SharpClawTestingHarnessServiceCollectionExtensions
{
    /// <summary>
    /// Adds scenario loading, scripted execution, oracle evaluation, and gate services.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpClawTestingHarness(this IServiceCollection services)
    {
        services.AddSingleton<IScenarioLoader, JsonScenarioLoader>();
        services.AddSingleton<IAgentScenarioExecutor, ScriptedScenarioAgentExecutor>();
        services.AddSingleton<ScenarioOracleFactory>();
        services.AddSingleton<ITraceWriter>(NullTraceWriter.Instance);
        services.AddSingleton<IScenarioRunner, ScenarioRunner>();
        services.AddSingleton<ScenarioGateEvaluator>();
        services.AddSingleton<ScenarioReportWriter>();
        services.AddSingleton<ScenarioResultStore>();
        return services;
    }
}
