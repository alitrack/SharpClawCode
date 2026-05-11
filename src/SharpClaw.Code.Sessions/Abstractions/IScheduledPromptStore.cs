using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Sessions.Abstractions;

/// <summary>
/// Persists durable scheduled prompt definitions for one workspace.
/// </summary>
public interface IScheduledPromptStore
{
    /// <summary>
    /// Lists all scheduled prompts for a workspace.
    /// </summary>
    Task<IReadOnlyList<ScheduledPromptDefinition>> ListAsync(string workspacePath, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one scheduled prompt by id.
    /// </summary>
    Task<ScheduledPromptDefinition?> GetByIdAsync(string workspacePath, string scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves one scheduled prompt definition.
    /// </summary>
    Task SaveAsync(string workspacePath, ScheduledPromptDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one scheduled prompt definition.
    /// </summary>
    Task<bool> DeleteAsync(string workspacePath, string scheduleId, CancellationToken cancellationToken);
}
