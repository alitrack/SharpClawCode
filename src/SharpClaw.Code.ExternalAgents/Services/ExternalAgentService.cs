using System.Text.Json;
using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Permissions.Abstractions;
using SharpClaw.Code.Permissions.Models;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Events;
using SharpClaw.Code.Protocol.Models;
using SharpClaw.Code.Protocol.Serialization;
using SharpClaw.Code.Sessions.Abstractions;
using SharpClaw.Code.Telemetry;
using SharpClaw.Code.Telemetry.Abstractions;

namespace SharpClaw.Code.ExternalAgents.Services;

/// <summary>
/// Coordinates external agent runs with permissions, sessions, and runtime events.
/// </summary>
public sealed class ExternalAgentService(
    IExternalAgentRegistry registry,
    IExternalAgentConfigProvider configProvider,
    IPermissionPolicyEngine permissionPolicyEngine,
    ISessionStore sessionStore,
    IRuntimeEventPublisher eventPublisher,
    IPathService pathService,
    ISystemClock systemClock) : IExternalAgentService
{
    /// <inheritdoc />
    public Task<ExternalAgentCatalogReport> ListAsync(string workspaceRoot, CancellationToken cancellationToken)
        => registry.BuildReportAsync(workspaceRoot, cancellationToken);

    /// <inheritdoc />
    public async Task<ExternalAgentRunResult> RunAsync(ExternalAgentRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AdapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

        var workspace = pathService.GetFullPath(request.WorkspacePath);
        var adapter = registry.Resolve(request.AdapterId);
        if (adapter is null)
        {
            return Failure(request.AdapterId, ExternalAgentFailureKind.UnknownAdapter, $"External agent adapter '{request.AdapterId}' is not registered.");
        }

        var session = await ResolveSessionAsync(workspace, request, cancellationToken).ConfigureAwait(false);
        var config = await configProvider.GetConfigAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (request.Mode == ExternalAgentMode.WorkspaceWrite && config.RequireApprovalForMutatingRuns)
        {
            var permission = await permissionPolicyEngine
                .EvaluateAsync(
                    CreateToolRequest(request, session.Id),
                    new PermissionEvaluationContext(
                        session.Id,
                        workspace,
                        workspace,
                        request.PermissionMode,
                        AllowedTools: null,
                        AllowDangerousBypass: request.PermissionMode == PermissionMode.DangerFullAccess,
                        IsInteractive: request.IsInteractive,
                        SourceKind: PermissionRequestSourceKind.Runtime,
                        SourceName: "external-agents",
                        TrustedPluginNames: null,
                        TrustedMcpServerNames: null,
                        PrimaryMode: request.PrimaryMode),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!permission.IsAllowed)
            {
                var denied = Failure(request.AdapterId, ExternalAgentFailureKind.PermissionDenied, permission.Reason ?? "External agent execution was denied.");
                await PublishFailedAsync(workspace, session.Id, request.AdapterId, denied, cancellationToken).ConfigureAwait(false);
                return denied;
            }
        }

        await PublishAsync(
            workspace,
            session.Id,
            new ExternalAgentRunStartedEvent(
                CreateIdentifier("event"),
                session.Id,
                null,
                systemClock.UtcNow,
                request.AdapterId,
                workspace,
                request.Mode),
            cancellationToken).ConfigureAwait(false);

        ExternalAgentRunResult result;
        try
        {
            result = await adapter.RunAsync(request with { WorkspacePath = workspace, SessionId = session.Id }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = new ExternalAgentRunResult(
                request.AdapterId,
                1,
                ex.ToString(),
                [],
                null,
                ExternalAgentFailureKind.Unexpected,
                ex.Message);
        }
        if (result.FailureKind is ExternalAgentFailureKind.None)
        {
            await PublishAsync(
                workspace,
                session.Id,
                new ExternalAgentRunCompletedEvent(
                    CreateIdentifier("event"),
                    session.Id,
                    null,
                    systemClock.UtcNow,
                    request.AdapterId,
                    result.ExitCode,
                    result.ExternalSessionId,
                    Truncate(result.OutputText)),
                cancellationToken).ConfigureAwait(false);
            await SaveSessionMetadataAsync(workspace, session, request.AdapterId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await PublishFailedAsync(workspace, session.Id, request.AdapterId, result, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<ConversationSession> ResolveSessionAsync(string workspace, ExternalAgentRunRequest request, CancellationToken cancellationToken)
    {
        ConversationSession? session = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            session = await sessionStore.GetByIdAsync(workspace, request.SessionId, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                throw new InvalidOperationException($"Session '{request.SessionId}' was not found.");
            }

            return session;
        }

        var created = new ConversationSession(
            CreateIdentifier("session"),
            "External agent session",
            SessionLifecycleState.Active,
            request.PermissionMode,
            OutputFormat.Text,
            workspace,
            workspace,
            systemClock.UtcNow,
            systemClock.UtcNow,
            null,
            null,
            new Dictionary<string, string>());
        await sessionStore.SaveAsync(workspace, created, cancellationToken).ConfigureAwait(false);
        await PublishAsync(
            workspace,
            created.Id,
            new SessionCreatedEvent(CreateIdentifier("event"), created.Id, null, systemClock.UtcNow, created),
            cancellationToken).ConfigureAwait(false);
        return created;
    }

    private static ToolExecutionRequest CreateToolRequest(ExternalAgentRunRequest request, string sessionId)
        => new(
            Guid.NewGuid().ToString("N"),
            sessionId,
            "external-agent",
            $"external-agent:{request.AdapterId}",
            JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["adapterId"] = request.AdapterId,
                    ["mode"] = request.Mode.ToString()
                },
                ProtocolJsonContext.Default.DictionaryStringString),
            ApprovalScope.ShellExecution,
            request.WorkspacePath,
            RequiresApproval: true,
            IsDestructive: request.Mode == ExternalAgentMode.WorkspaceWrite);

    private async Task SaveSessionMetadataAsync(string workspace, ConversationSession session, string adapterId, CancellationToken cancellationToken)
    {
        var metadata = session.Metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(session.Metadata, StringComparer.Ordinal);
        metadata[SharpClawWorkflowMetadataKeys.LastExternalAgentId] = adapterId;
        await sessionStore.SaveAsync(workspace, session with { Metadata = metadata, UpdatedAtUtc = systemClock.UtcNow }, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishFailedAsync(string workspace, string sessionId, string adapterId, ExternalAgentRunResult result, CancellationToken cancellationToken)
        => await PublishAsync(
            workspace,
            sessionId,
            new ExternalAgentRunFailedEvent(
                CreateIdentifier("event"),
                sessionId,
                null,
                systemClock.UtcNow,
                adapterId,
                result.FailureKind,
                result.Error ?? "External agent run failed."),
            cancellationToken).ConfigureAwait(false);

    private ValueTask PublishAsync(string workspace, string sessionId, RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        => eventPublisher.PublishAsync(
            runtimeEvent,
            new RuntimeEventPublishOptions(workspace, sessionId, PersistToSessionStore: true),
            cancellationToken);

    private static ExternalAgentRunResult Failure(string adapterId, ExternalAgentFailureKind kind, string error)
        => new(adapterId, 1, string.Empty, [], null, kind, error);

    private static string CreateIdentifier(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private static string Truncate(string? value, int max = 1000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= max ? value : value[..max];
    }
}
