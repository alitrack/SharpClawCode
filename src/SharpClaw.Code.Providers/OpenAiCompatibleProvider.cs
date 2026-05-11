using System.Collections.Concurrent;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using SharpClaw.Code.Infrastructure.Abstractions;
using SharpClaw.Code.Providers.Abstractions;
using SharpClaw.Code.Providers.Configuration;
using SharpClaw.Code.Providers.Internal;
using SharpClaw.Code.Providers.Models;
using SharpClaw.Code.Protocol.Models;

namespace SharpClaw.Code.Providers;

/// <summary>
/// Streams responses from an OpenAI-compatible chat completions API using Microsoft.Extensions.AI and the OpenAI .NET SDK.
/// </summary>
public sealed class OpenAiCompatibleProvider(
    IOptions<OpenAiCompatibleProviderOptions> options,
    IProviderCredentialStore credentialStore,
    ISystemClock systemClock,
    ILogger<OpenAiCompatibleProvider> logger) : IModelProvider
{
    private readonly OpenAiCompatibleProviderOptions _options = options.Value;
    private readonly ConcurrentDictionary<OpenAiClientCacheKey, OpenAIClient> _clientCache = new();
    internal const string RuntimeProfileMetadataKey = "openai-compatible.profile";

    /// <inheritdoc />
    public string ProviderName => _options.ProviderName;

    /// <inheritdoc />
    public bool SupportsImageInput => _options.SupportsImageInput;

    /// <inheritdoc />
    public async Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveCredentialAsync(cancellationToken).ConfigureAwait(false);
        return Internal.ProviderAuthStatusFactory.FromConfiguration(
            ProviderName,
            resolved.ApiKey,
            _options.AuthMode,
            _options.LocalRuntimes.Values.Any(static runtime => runtime.AuthMode != ProviderAuthMode.ApiKey),
            sourceType: resolved.SourceType ?? (string.IsNullOrWhiteSpace(_options.ApiKey) ? null : "config"),
            statusDetail: resolved.StatusDetail ?? (string.IsNullOrWhiteSpace(_options.ApiKey) ? null : "configured API key"));
    }

    /// <inheritdoc />
    public async Task<ProviderStreamHandle> StartStreamAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Starting OpenAI-compatible MEAI stream for request {RequestId}.", request.Id);
        var resolved = await ResolveCredentialAsync(cancellationToken).ConfigureAwait(false);
        return new ProviderStreamHandle(request, StreamEventsAsync(request, resolved.ApiKey, cancellationToken));
    }

    private async IAsyncEnumerable<ProviderEvent> StreamEventsAsync(
        ProviderRequest request,
        string? resolvedApiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var profile = ResolveProfile(request.Metadata);
        var modelId = Internal.ProviderHttpHelpers.ResolveModelOrDefault(
            request.Model,
            profile?.DefaultChatModel ?? _options.DefaultModel);
        var openAiClient = GetOrCreateOpenAiClient(profile, resolvedApiKey);
        var nativeClient = openAiClient.GetChatClient(modelId);
        using var chatClient = nativeClient.AsIChatClient();

        var messages = request.Messages is not null
            ? OpenAiMessageBuilder.BuildMessages(request.Messages, request.SystemPrompt)
            : BuildChatMessages(request);

        var chatOptions = new ChatOptions();
        if (request.Temperature is { } temp)
        {
            chatOptions.Temperature = (float)temp;
        }

        if (request.MaxTokens is { } maxTokens)
        {
            chatOptions.MaxOutputTokens = maxTokens;
        }

        if (request.Tools is { Count: > 0 } toolDefs)
        {
            chatOptions.Tools = OpenAiMessageBuilder.BuildTools(toolDefs);
        }

        var updates = chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken);
        await foreach (var ev in OpenAiMeaiStreamAdapter.AdaptAsync(updates, request.Id, systemClock, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return ev;
        }
    }

    private OpenAIClient GetOrCreateOpenAiClient(LocalRuntimeProfileOptions? profile, string? resolvedApiKey)
    {
        var normalized = Internal.ProviderHttpHelpers.NormalizeBaseUrl(profile?.BaseUrl ?? _options.BaseUrl);
        var apiKey = profile?.ApiKey ?? resolvedApiKey ?? _options.ApiKey ?? "local-runtime";
        var cacheKey = new OpenAiClientCacheKey(normalized, ComputeCredentialFingerprint(apiKey));
        return _clientCache.GetOrAdd(
            cacheKey,
            static (_, state) =>
            {
                var openAiOptions = new OpenAIClientOptions();
                if (state.NormalizedEndpoint is not null)
                {
                    openAiOptions.Endpoint = new Uri(state.NormalizedEndpoint);
                }

                return new OpenAIClient(new ApiKeyCredential(state.ApiKey), openAiOptions);
            },
            (NormalizedEndpoint: normalized, ApiKey: apiKey));
    }

    private static string ComputeCredentialFingerprint(string apiKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    private static List<Microsoft.Extensions.AI.ChatMessage> BuildChatMessages(ProviderRequest request)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, request.SystemPrompt));
        }

        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Prompt));
        return messages;
    }

    private LocalRuntimeProfileOptions? ResolveProfile(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue(RuntimeProfileMetadataKey, out var profileName)
            || string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        return _options.LocalRuntimes.TryGetValue(profileName, out var profile)
            ? profile
            : null;
    }

    private async Task<ResolvedProviderCredential> ResolveCredentialAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ResolvedProviderCredential(_options.ApiKey, "config", "configured API key");
        }

        return await credentialStore.ResolveAsync(ProviderName, cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct OpenAiClientCacheKey(string? Endpoint, string CredentialFingerprint);
}
