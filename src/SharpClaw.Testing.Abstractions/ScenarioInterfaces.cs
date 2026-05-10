namespace SharpClaw.Testing.Abstractions;

/// <summary>
/// Executes one scenario and evaluates its oracles.
/// </summary>
public interface IScenarioRunner
{
    /// <summary>
    /// Runs a scenario and returns the complete result.
    /// </summary>
    /// <param name="scenario">Scenario to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scenario run result.</returns>
    Task<ScenarioRunResult> RunAsync(AgentScenario scenario, CancellationToken cancellationToken);
}

/// <summary>
/// Executes scenario input and produces an agent run trace.
/// </summary>
public interface IAgentScenarioExecutor
{
    /// <summary>
    /// Executes a scenario and captures a trace.
    /// </summary>
    /// <param name="scenario">Scenario to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The captured run trace.</returns>
    Task<AgentRunTrace> ExecuteAsync(AgentScenario scenario, CancellationToken cancellationToken);
}

/// <summary>
/// Evaluates an explicit scenario expectation against a trace.
/// </summary>
public interface IScenarioOracle
{
    /// <summary>
    /// Gets the oracle display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the oracle.
    /// </summary>
    /// <param name="scenario">Scenario under evaluation.</param>
    /// <param name="trace">Trace to inspect.</param>
    /// <returns>The oracle result.</returns>
    OracleResult Evaluate(AgentScenario scenario, AgentRunTrace trace);
}

/// <summary>
/// Loads scenarios from files or directories.
/// </summary>
public interface IScenarioLoader
{
    /// <summary>
    /// Loads a single scenario JSON file.
    /// </summary>
    /// <param name="path">Scenario file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded scenario.</returns>
    Task<AgentScenario> LoadFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Loads every scenario JSON file in a directory.
    /// </summary>
    /// <param name="directory">Scenario directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded scenarios ordered by id.</returns>
    Task<IReadOnlyList<AgentScenario>> LoadDirectoryAsync(string directory, CancellationToken cancellationToken);
}

/// <summary>
/// Writes captured traces for later inspection or replay.
/// </summary>
public interface ITraceWriter
{
    /// <summary>
    /// Writes a trace and returns its path.
    /// </summary>
    /// <param name="trace">Trace to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The written trace path, or <c>null</c> when no file was written.</returns>
    Task<string?> WriteAsync(AgentRunTrace trace, CancellationToken cancellationToken);
}
