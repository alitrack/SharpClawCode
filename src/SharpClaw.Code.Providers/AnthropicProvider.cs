using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Providers.Abstractions;
using SharpClaw.Code.Providers.Configuration;
using SharpClaw.Code.Providers.Internal;
using SharpClaw.Code.Providers.Models;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Providers;

/// <summary>
/// Streams responses from Anthropic's Messages API using the official Anthropic C# SDK.
/// </summary>
public sealed class AnthropicProvider(
    IOptions<AnthropicProviderOptions> options,
    ISystemClock systemClock,
    ILogger<AnthropicProvider> logger) : IModelProvider
{
    private readonly AnthropicProviderOptions _options = options.Value;

    /// <inheritdoc />
    public string ProviderName => _options.ProviderName;

    /// <inheritdoc />
    public Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(Internal.ProviderAuthStatusFactory.FromConfiguration(
            ProviderName,
            _options.ApiKey,
            ProviderAuthMode.ApiKey,
            hasAuthOptionalRuntime: false));

    /// <inheritdoc />
    public Task<ProviderStreamHandle> StartStreamAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = CreateClient();
        var modelId = Internal.ProviderHttpHelpers.ResolveModelOrDefault(request.Model, _options.DefaultModel);

        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt;
        float? temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : null;

        MessageCreateParams parameters;

        if (request.Messages is not null)
        {
            var messages = Internal.AnthropicMessageBuilder.BuildMessages(request.Messages);
            parameters = new MessageCreateParams
            {
                MaxTokens = request.MaxTokens ?? 1024,
                Model = modelId,
                Messages = messages,
                Temperature = temperature,
            };

            if (request.Tools is { Count: > 0 } tools)
            {
                var anthropicTools = Internal.AnthropicMessageBuilder.BuildTools(tools);
                parameters = parameters with { Tools = anthropicTools };
            }
        }
        else
        {
            parameters = new MessageCreateParams
            {
                MaxTokens = request.MaxTokens ?? 1024,
                Model = modelId,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = request.Prompt,
                    },
                ],
                Temperature = temperature,
            };
        }

        if (systemPrompt is not null)
        {
            parameters = parameters with { System = systemPrompt };
        }

        logger.LogInformation("Starting Anthropic SDK stream for request {RequestId}.", request.Id);

        IAsyncEnumerable<RawMessageStreamEvent> stream;
        try
        {
            stream = client.Messages.CreateStreaming(parameters, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (ProviderStreamFailureClassifier.IsAuthenticationFailure(exception))
        {
            throw new ProviderExecutionException(
                ProviderName,
                modelId,
                ProviderFailureKind.AuthenticationUnavailable,
                $"Provider '{ProviderName}' authentication failed while starting the stream.",
                exception);
        }

        logger.LogInformation("Started Anthropic SDK stream for request {RequestId}.", request.Id);

        return Task.FromResult(new ProviderStreamHandle(request, AnthropicSdkStreamAdapter.AdaptAsync(stream, request.Id, systemClock, cancellationToken)));
    }

    private AnthropicClient CreateClient()
    {
        var apiKey = _options.ApiKey ?? string.Empty;
        var clientOptions = new ClientOptions
        {
            ApiKey = apiKey,
        };

        var normalized = Internal.ProviderHttpHelpers.NormalizeBaseUrl(_options.BaseUrl);
        if (normalized is not null)
        {
            clientOptions.BaseUrl = normalized;
        }

        return new AnthropicClient(clientOptions);
    }
}
