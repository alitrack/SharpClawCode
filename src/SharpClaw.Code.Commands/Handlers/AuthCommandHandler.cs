using System.CommandLine;
using System.Text;
using System.Text.Json;
using SharpClaw.Code.Commands.Models;
using SharpClaw.Code.Commands.Options;
using SharpClaw.Code.Providers.Abstractions;
using SharpClaw.Code.Protocol.Commands;

namespace SharpClaw.Code.Commands;

/// <summary>
/// Shows and updates user-scoped provider credential references.
/// </summary>
public sealed class AuthCommandHandler(
    IProviderCredentialStore providerCredentialStore,
    IProviderCatalogService providerCatalogService,
    ICliInvocationEnvironment cliInvocationEnvironment,
    OutputRendererDispatcher outputRendererDispatcher) : ICommandHandler, ISlashCommandHandler
{
    /// <inheritdoc />
    public string Name => "auth";

    /// <inheritdoc />
    public string Description => "Shows provider auth status and manages user-scoped BYOAK credentials.";

    /// <inheritdoc />
    public string CommandName => Name;

    /// <inheritdoc />
    public Command BuildCommand(GlobalCliOptions globalOptions)
    {
        var command = new Command(Name, Description);

        var status = new Command("status", "Shows provider authentication status.");
        var statusProvider = new Option<string?>("--provider") { Description = "Optional provider name to inspect." };
        status.Options.Add(statusProvider);
        status.SetAction((parseResult, cancellationToken) => ExecuteStatusAsync(
            globalOptions.Resolve(parseResult),
            parseResult.GetValue(statusProvider),
            cancellationToken));
        command.Subcommands.Add(status);

        var list = new Command("list", "Lists stored credential descriptors without revealing secret material.");
        list.SetAction((parseResult, cancellationToken) => ExecuteListAsync(globalOptions.Resolve(parseResult), cancellationToken));
        command.Subcommands.Add(list);

        var setKey = new Command("set-key", "Stores a credential reference for one provider.");
        var providerOption = new Option<string>("--provider") { Required = true, Description = "Provider name." };
        var envVarOption = new Option<string?>("--env-var") { Description = "Environment variable name to read the API key from." };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the API key from standard input." };
        setKey.Options.Add(providerOption);
        setKey.Options.Add(envVarOption);
        setKey.Options.Add(stdinOption);
        setKey.SetAction((parseResult, cancellationToken) => ExecuteSetKeyAsync(
            parseResult.GetValue(providerOption) ?? throw new InvalidOperationException("--provider is required."),
            parseResult.GetValue(envVarOption),
            parseResult.GetValue(stdinOption),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        command.Subcommands.Add(setKey);

        var clearKey = new Command("clear-key", "Clears the stored credential reference for one provider.");
        var clearProviderOption = new Option<string>("--provider") { Required = true, Description = "Provider name." };
        clearKey.Options.Add(clearProviderOption);
        clearKey.SetAction((parseResult, cancellationToken) => ExecuteClearKeyAsync(
            parseResult.GetValue(clearProviderOption) ?? throw new InvalidOperationException("--provider is required."),
            globalOptions.Resolve(parseResult),
            cancellationToken));
        command.Subcommands.Add(clearKey);

        command.SetAction((parseResult, cancellationToken) => ExecuteStatusAsync(globalOptions.Resolve(parseResult), null, cancellationToken));
        return command;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(SlashCommandParseResult command, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        if (command.Arguments.Length == 0 || string.Equals(command.Arguments[0], "status", StringComparison.OrdinalIgnoreCase))
        {
            var provider = command.Arguments.Length >= 2 ? command.Arguments[1] : null;
            return ExecuteStatusAsync(context, provider, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteListAsync(context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "clear-key", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2)
        {
            return ExecuteClearKeyAsync(command.Arguments[1], context, cancellationToken);
        }

        if (string.Equals(command.Arguments[0], "set-key", StringComparison.OrdinalIgnoreCase) && command.Arguments.Length >= 2)
        {
            if (command.Arguments.Length >= 4 && string.Equals(command.Arguments[2], "--env-var", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteSetKeyAsync(command.Arguments[1], command.Arguments[3], false, context, cancellationToken);
            }

            if (command.Arguments.Length >= 3 && string.Equals(command.Arguments[2], "--stdin", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteSetKeyAsync(command.Arguments[1], null, true, context, cancellationToken);
            }

            return ExecuteSetKeyAsync(command.Arguments[1], null, false, context, cancellationToken);
        }

        return RenderAsync("Usage: /auth [status [provider]|list|set-key <provider> [--env-var NAME|--stdin]|clear-key <provider>]", context, cancellationToken, success: false);
    }

    private async Task<int> ExecuteStatusAsync(CommandExecutionContext context, string? providerName, CancellationToken cancellationToken)
    {
        var entries = await providerCatalogService.ListAsync(cancellationToken).ConfigureAwait(false);
        var filtered = string.IsNullOrWhiteSpace(providerName)
            ? entries
            : entries.Where(entry => string.Equals(entry.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (filtered.Count == 0)
        {
            return await RenderAsync($"No provider '{providerName}' was found.", context, cancellationToken, success: false).ConfigureAwait(false);
        }

        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"{filtered.Count} provider auth status entr{(filtered.Count == 1 ? "y" : "ies")}.", JsonSerializer.Serialize(filtered)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteListAsync(CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var entries = await providerCredentialStore.ListAsync(cancellationToken).ConfigureAwait(false);
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(true, 0, context.OutputFormat, $"{entries.Count} stored credential descriptor(s).", JsonSerializer.Serialize(entries)),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ExecuteSetKeyAsync(
        string providerName,
        string? environmentVariableName,
        bool useStdin,
        CommandExecutionContext context,
        CancellationToken cancellationToken)
    {
        await EnsureProviderExistsAsync(providerName, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(environmentVariableName))
        {
            await providerCredentialStore.SetEnvironmentVariableAsync(providerName, environmentVariableName.Trim(), cancellationToken).ConfigureAwait(false);
            return await RenderAsync(
                $"Stored credential reference for provider '{providerName}' via environment variable {environmentVariableName.Trim()}.",
                context,
                cancellationToken).ConfigureAwait(false);
        }

        string secret;
        if (useStdin)
        {
            secret = (await cliInvocationEnvironment.ReadStandardInputToEndAsync(cancellationToken).ConfigureAwait(false)).Trim();
        }
        else if (!cliInvocationEnvironment.IsInputRedirected)
        {
            secret = ReadSecretFromConsole($"Enter API key for {providerName}: ");
        }
        else
        {
            return await RenderAsync("Provide --env-var, pass --stdin, or run interactively to enter a secret without exposing it on the command line.", context, cancellationToken, success: false).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return await RenderAsync("No API key value was provided.", context, cancellationToken, success: false).ConfigureAwait(false);
        }

        await providerCredentialStore.SetProtectedSecretAsync(providerName, secret, cancellationToken).ConfigureAwait(false);
        return await RenderAsync(
            $"Stored a protected local credential for provider '{providerName}'.",
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteClearKeyAsync(string providerName, CommandExecutionContext context, CancellationToken cancellationToken)
    {
        var removed = await providerCredentialStore.ClearAsync(providerName, cancellationToken).ConfigureAwait(false);
        return await RenderAsync(
            removed ? $"Cleared the stored credential reference for provider '{providerName}'." : $"No stored credential reference was found for provider '{providerName}'.",
            context,
            cancellationToken,
            removed).ConfigureAwait(false);
    }

    private async Task EnsureProviderExistsAsync(string providerName, CancellationToken cancellationToken)
    {
        var providers = await providerCatalogService.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!providers.Any(entry => string.Equals(entry.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Unknown provider '{providerName}'.");
        }
    }

    private async Task<int> RenderAsync(string message, CommandExecutionContext context, CancellationToken cancellationToken, bool success = true)
    {
        await outputRendererDispatcher.RenderCommandResultAsync(
            new CommandResult(success, success ? 0 : 1, context.OutputFormat, message, null),
            context.OutputFormat,
            cancellationToken).ConfigureAwait(false);
        return success ? 0 : 1;
    }

    private static string ReadSecretFromConsole(string prompt)
    {
        Console.Write(prompt);
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }
}
