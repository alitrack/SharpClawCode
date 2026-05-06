using SharpClaw.Code.Protocol.Commands;

namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Executes research-mode prompt flows through the standard runtime.
/// </summary>
public interface IResearchWorkflowService
{
    /// <summary>
    /// Runs a research-mode prompt.
    /// </summary>
    Task<TurnExecutionResult> ExecuteAsync(string prompt, RuntimeCommandContext context, CancellationToken cancellationToken);
}
