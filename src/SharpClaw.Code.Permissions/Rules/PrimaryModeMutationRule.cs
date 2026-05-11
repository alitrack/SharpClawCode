using SharpClaw.Code.Permissions.Abstractions;
using SharpClaw.Code.Permissions.Models;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Permissions.Rules;

/// <summary>
/// Blocks mutating tool executions while the session is in a read-only workflow mode.
/// </summary>
public sealed class PrimaryModeMutationRule : IPermissionRule
{
    /// <inheritdoc />
    public Task<PermissionRuleResult> EvaluateAsync(
        ToolExecutionRequest request,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        if (context.PrimaryMode is not (PrimaryMode.Plan or PrimaryMode.Research))
        {
            return Task.FromResult(PermissionRuleResult.Abstain());
        }

        var modeLabel = context.PrimaryMode == PrimaryMode.Research ? "Research mode" : "Plan mode";

        if (request.IsDestructive)
        {
            return Task.FromResult(PermissionRuleResult.Deny($"{modeLabel} blocks mutating tools."));
        }

        if (request.ApprovalScope is ApprovalScope.FileSystemWrite or ApprovalScope.ShellExecution)
        {
            return Task.FromResult(PermissionRuleResult.Deny($"{modeLabel} blocks {request.ApprovalScope}."));
        }

        return Task.FromResult(PermissionRuleResult.Abstain());
    }
}
