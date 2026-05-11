using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Runtime.Workflow;

/// <inheritdoc />
public sealed class ResearchWorkflowService(
    IRuntimeCommandService runtimeCommandService) : IResearchWorkflowService
{
    private const string ResearchPrefix = """
        Research mode is active.

        Produce a citation-oriented answer with:
        - concise findings
        - clearly attributed sources
        - confidence notes where uncertainty remains
        - unresolved questions when evidence is incomplete
        """;

    /// <inheritdoc />
    public Task<TurnExecutionResult> ExecuteAsync(string prompt, RuntimeCommandContext context, CancellationToken cancellationToken)
        => runtimeCommandService.ExecutePromptAsync(
            $"{ResearchPrefix}{Environment.NewLine}{Environment.NewLine}{prompt.Trim()}",
            context with
            {
                PrimaryMode = PrimaryMode.Research,
                PermissionMode = PermissionMode.ReadOnly,
            },
            cancellationToken);
}
