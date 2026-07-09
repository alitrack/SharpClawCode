using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.ExternalAgents.Adapters;

/// <summary>
/// Claude Code text-mode adapter.
/// </summary>
public sealed class ClaudeCodeAdapter(IExternalAgentExecutableResolver executableResolver, IProcessRunner processRunner, IExternalAgentConfigProvider configProvider)
    : ExternalAgentAdapterBase(executableResolver, processRunner, configProvider)
{
    /// <inheritdoc />
    public override ExternalAgentDescriptor Descriptor { get; } = Create("claude", "Claude Code", "claude", supportsResume: true);

    /// <inheritdoc />
    protected override string[] BuildArguments(ExternalAgentRunRequest request) => ["-p", request.Prompt];

    private static ExternalAgentDescriptor Create(string id, string name, string executable, bool supportsResume)
        => new(id, name, executable, [ExternalAgentMode.ReadOnly, ExternalAgentMode.WorkspaceWrite], false, false, true, supportsResume, ["textPrompt"]);
}

/// <summary>
/// OpenCode text-mode adapter.
/// </summary>
public sealed class OpenCodeAdapter(IExternalAgentExecutableResolver executableResolver, IProcessRunner processRunner, IExternalAgentConfigProvider configProvider)
    : ExternalAgentAdapterBase(executableResolver, processRunner, configProvider)
{
    /// <inheritdoc />
    public override ExternalAgentDescriptor Descriptor { get; } = new(
        "opencode",
        "OpenCode",
        "opencode",
        [ExternalAgentMode.ReadOnly, ExternalAgentMode.WorkspaceWrite],
        false,
        false,
        true,
        true,
        ["textPrompt"]);

    /// <inheritdoc />
    protected override string[] BuildArguments(ExternalAgentRunRequest request) => ["run", request.Prompt];
}

/// <summary>
/// Gemini CLI text-mode adapter.
/// </summary>
public sealed class GeminiCliAdapter(IExternalAgentExecutableResolver executableResolver, IProcessRunner processRunner, IExternalAgentConfigProvider configProvider)
    : ExternalAgentAdapterBase(executableResolver, processRunner, configProvider)
{
    /// <inheritdoc />
    public override ExternalAgentDescriptor Descriptor { get; } = new(
        "gemini",
        "Gemini CLI",
        "gemini",
        [ExternalAgentMode.ReadOnly, ExternalAgentMode.WorkspaceWrite],
        false,
        false,
        true,
        false,
        ["textPrompt"]);

    /// <inheritdoc />
    protected override string[] BuildArguments(ExternalAgentRunRequest request) => ["--prompt", request.Prompt];
}

/// <summary>
/// Codex CLI text-mode adapter.
/// </summary>
public sealed class CodexCliAdapter(IExternalAgentExecutableResolver executableResolver, IProcessRunner processRunner, IExternalAgentConfigProvider configProvider)
    : ExternalAgentAdapterBase(executableResolver, processRunner, configProvider)
{
    /// <inheritdoc />
    public override ExternalAgentDescriptor Descriptor { get; } = new(
        "codex",
        "Codex CLI",
        "codex",
        [ExternalAgentMode.ReadOnly, ExternalAgentMode.WorkspaceWrite],
        false,
        false,
        true,
        true,
        ["textPrompt"]);

    /// <inheritdoc />
    protected override string[] BuildArguments(ExternalAgentRunRequest request) => ["exec", request.Prompt];
}
