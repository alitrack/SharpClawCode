using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <summary>
/// Polls due scheduled prompts for the current workspace process.
/// </summary>
public sealed class ScheduledPromptRunner(
    IScheduledPromptService scheduledPromptService,
    IPathService pathService,
    ILogger<ScheduledPromptRunner> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workspaceRoot = pathService.GetFullPath(pathService.GetCurrentDirectory());
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                try
                {
                    await scheduledPromptService.RunDueAsync(
                        workspaceRoot,
                        new RuntimeCommandContext(
                            WorkingDirectory: workspaceRoot,
                            Model: null,
                            PermissionMode: PermissionMode.WorkspaceWrite,
                            OutputFormat: OutputFormat.Text,
                            PrimaryMode: PrimaryMode.Build,
                            SessionId: null,
                            AgentId: null,
                            IsInteractive: false),
                        stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Scheduled prompt polling failed for workspace {WorkspaceRoot}.", workspaceRoot);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        finally
        {
            timer.Dispose();
        }
    }
}
