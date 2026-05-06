using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Runtime.Abstractions;

/// <summary>
/// Manages durable scheduled prompts and executes due work through the standard runtime.
/// </summary>
public interface IScheduledPromptService
{
    /// <summary>
    /// Lists schedules for the workspace.
    /// </summary>
    Task<IReadOnlyList<ScheduledPromptDefinition>> ListAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a schedule by id.
    /// </summary>
    Task<ScheduledPromptDefinition?> GetAsync(string workspaceRoot, string scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a schedule definition.
    /// </summary>
    Task<ScheduledPromptDefinition> SaveAsync(string workspaceRoot, ScheduledPromptDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a schedule definition.
    /// </summary>
    Task<bool> RemoveAsync(string workspaceRoot, string scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Enables or disables a schedule.
    /// </summary>
    Task<ScheduledPromptDefinition> SetEnabledAsync(string workspaceRoot, string scheduleId, bool enabled, CancellationToken cancellationToken);

    /// <summary>
    /// Executes one schedule immediately.
    /// </summary>
    Task<ScheduledPromptRunReport> RunAsync(string workspaceRoot, string scheduleId, RuntimeCommandContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Executes all due schedules for a workspace.
    /// </summary>
    Task<IReadOnlyList<ScheduledPromptRunReport>> RunDueAsync(string workspaceRoot, RuntimeCommandContext context, CancellationToken cancellationToken);
}
