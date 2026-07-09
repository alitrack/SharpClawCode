using FluentAssertions;
using SharpClaw.Code.ExternalAgents.Abstractions;
using SharpClaw.Code.ExternalAgents.Adapters;
using SharpClaw.Code.Infrastructure.Models;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.UnitTests.ExternalAgents;

public sealed class ExternalAgentAdapterTests
{
    [Fact]
    public async Task Codex_adapter_should_build_text_mode_command_and_map_output()
    {
        var runner = new RecordingProcessRunner();
        var adapter = new CodexCliAdapter(new FixedExecutableResolver("/bin/codex"), runner, new EnabledConfigProvider());

        var result = await adapter.RunAsync(
            new ExternalAgentRunRequest("codex", "/workspace", "review this", ExternalAgentMode.WorkspaceWrite),
            CancellationToken.None);

        result.FailureKind.Should().Be(ExternalAgentFailureKind.None);
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Arguments.Should().Equal("exec", "review this");
        result.OutputText.Should().Contain("ok");
    }

    private sealed class FixedExecutableResolver(string path) : IExternalAgentExecutableResolver
    {
        public string? Resolve(string executableNameOrPath) => path;
    }

    private sealed class EnabledConfigProvider : IExternalAgentConfigProvider
    {
        public Task<ExternalAgentsConfig> GetConfigAsync(string workspaceRoot, CancellationToken cancellationToken)
            => Task.FromResult(new ExternalAgentsConfig(Enabled: true));
    }

    private sealed class RecordingProcessRunner : SharpClaw.Code.Infrastructure.Abstractions.IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ProcessRunResult(0, "ok", string.Empty, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }
    }
}
