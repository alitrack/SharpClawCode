using System.CommandLine;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Providers.Models;
using SharpClaw.Code.Protocol.Commands;
using SharpClaw.Code.Protocol.Enums;
using SharpClaw.Code.Runtime.Abstractions;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Executes a prompt in read-only research mode.
/// </summary>
public sealed class ResearchCommandHandler(
    IResearchWorkflowService researchWorkflowService,
    OutputRendererDispatcher outputRendererDispatcher,
    ICliInvocationEnvironment cliInvocationEnvironment) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "research";

    /// <inheritdoc />
    public string Description => "Runs a prompt in citation-oriented read-only research mode.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);
        var promptArgument = new Argument<string[]>("prompt")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Research prompt text."
        };
        command.Arguments.Add(promptArgument);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            globalOptions.Resolve(parseResult),
            parseResult.GetValue(promptArgument) ?? [],
            cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
        => ExecuteAsync(context, command.Arguments, cancellationToken);

    private async Task<int> ExecuteAsync(
        CommandExecutionContext context,
        IReadOnlyList<string> promptTokens,
        CancellationToken cancellationToken)
    {
        var prompt = await BuildPromptAsync(promptTokens, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(false, 1, context.OutputFormat, "No research prompt text was provided.", null),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var isInteractive = !cliInvocationEnvironment.IsInputRedirected
            && !cliInvocationEnvironment.IsOutputRedirected
            && context.OutputFormat == OutputFormat.Text;

        try
        {
            var result = await researchWorkflowService
                .ExecuteAsync(
                    prompt,
                    context.ToRuntimeCommandContext(
                        isInteractive: isInteractive,
                        primaryModeOverride: PrimaryMode.Research,
                        permissionModeOverride: PermissionMode.ReadOnly),
                    cancellationToken)
                .ConfigureAwait(false);
            await outputRendererDispatcher.RenderTurnExecutionResultAsync(result, context.OutputFormat, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (ProviderExecutionException exception)
        {
            await outputRendererDispatcher.RenderCommandResultAsync(
                new CommandResult(false, 1, context.OutputFormat, $"Provider failure ({exception.Kind}): {exception.Message}", null),
                context.OutputFormat,
                cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    private async Task<string> BuildPromptAsync(IReadOnlyList<string> promptTokens, CancellationToken cancellationToken)
    {
        var promptText = string.Join(' ', promptTokens).Trim();
        var stdinText = cliInvocationEnvironment.IsInputRedirected
            ? (await cliInvocationEnvironment.ReadStandardInputToEndAsync(cancellationToken).ConfigureAwait(false)).Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(stdinText))
        {
            return promptText;
        }

        return string.IsNullOrWhiteSpace(promptText)
            ? stdinText
            : $"Piped input:{Environment.NewLine}{stdinText}{Environment.NewLine}{Environment.NewLine}Research request:{Environment.NewLine}{promptText}";
    }
}
