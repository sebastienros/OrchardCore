using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Cli;

internal sealed class CliApplication
{
    private static readonly TimeSpan ManifestTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OpenApiTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DocumentationTtl = TimeSpan.FromHours(12);
    private static readonly Uri DocumentationIndexUri = new("https://docs.orchardcore.net/en/latest/search/search_index.json");
    private readonly RootCommand _rootCommand;
    private readonly CliPaths _paths;
    private readonly HttpClient _httpClient;
    private readonly ContextStore _contextStore;
    private readonly CacheService _cacheService;
    private readonly ICredentialStore _credentialStore;
    private readonly OAuthClient _oauthClient;
    private readonly CliConfiguration _configuration;
    private readonly Option<string?> _outputOption;
    private readonly Option<string?> _contextOption;

    private CliApplication(
        RootCommand rootCommand,
        CliPaths paths,
        HttpClient httpClient,
        ContextStore contextStore,
        CacheService cacheService,
        ICredentialStore credentialStore,
        OAuthClient oauthClient,
        CliConfiguration configuration,
        Option<string?> outputOption,
        Option<string?> contextOption)
    {
        _rootCommand = rootCommand;
        _paths = paths;
        _httpClient = httpClient;
        _contextStore = contextStore;
        _cacheService = cacheService;
        _credentialStore = credentialStore;
        _oauthClient = oauthClient;
        _configuration = configuration;
        _outputOption = outputOption;
        _contextOption = contextOption;
    }

    public static async Task<CliApplication> CreateAsync(string[] args, CliPaths paths, HttpClient httpClient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(httpClient);

        var contextStore = new ContextStore(paths);
        var cacheService = new CacheService(paths);
        var credentialStore = CredentialStoreFactory.CreateDefault();
        var configuration = await contextStore.LoadAsync(cancellationToken);
        var oauthClient = new OAuthClient(httpClient, Console.Error);

        var outputOption = new Option<string?>("--output")
        {
            Description = "Output format: json, table, csv, tsv, yaml, toml, none",
            DefaultValueFactory = _ => "json",
            Recursive = true,
        };

        var contextOption = new Option<string?>("--context", "-c")
        {
            Description = "Named context to use",
            Recursive = true,
        };

        var root = new RootCommand("Orchard Core remote management CLI");
        root.Options.Add(outputOption);
        root.Options.Add(contextOption);

        var app = new CliApplication(root, paths, httpClient, contextStore, cacheService, credentialStore, oauthClient, configuration, outputOption, contextOption);
        app.AddStaticCommands();
        await app.AddDynamicCommandsAsync(args, cancellationToken);
        return app;
    }

    public async Task<int> InvokeAsync(string[] args, TextWriter? errorWriter = null)
    {
        try
        {
            return await _rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false,
            });
        }
        catch (ApiException exception)
        {
            await WriteApiErrorAsync(exception);
            return 4;
        }
        catch (CliException exception)
        {
            await (errorWriter ?? Console.Error).WriteLineAsync($"Error: {exception.Message}");
            return 1;
        }
        catch (HttpRequestException exception)
        {
            await WriteErrorAsync("http_error", exception.Message);
            return 2;
        }
        catch (JsonException exception)
        {
            await WriteErrorAsync("invalid_json", exception.Message);
            return 3;
        }
    }

    private void AddStaticCommands()
    {
        _rootCommand.Subcommands.Add(CreateLoginCommand());
        _rootCommand.Subcommands.Add(CreateLogoutCommand());
        _rootCommand.Subcommands.Add(CreateContextCommand());
        _rootCommand.Subcommands.Add(CreateApiCommand());
        _rootCommand.Subcommands.Add(CreateDocsCommand());
        _rootCommand.Subcommands.Add(CreateCompletionCommand());
        _rootCommand.Subcommands.Add(CreateDoctorCommand());
        _rootCommand.Subcommands.Add(CreateVersionCommand());
    }

    private async Task AddDynamicCommandsAsync(string[] args, CancellationToken cancellationToken)
    {
        if (IsStaticCommandRequest(args))
        {
            return;
        }

        var requestedContext = ReadRequestedContext(args);
        var context = ContextStore.FindContext(_configuration, requestedContext);
        if (context is null)
        {
            return;
        }

        var cacheKey = context.TenantUrl;
        var cached = await _cacheService.ReadAsync(cacheKey, CacheKind.OpenApi, cancellationToken);
        if (cached is null || cached.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            try
            {
                _ = await RefreshOpenApiAsync(context, force: false, allowStale: false, cancellationToken);
                cached = await _cacheService.ReadAsync(cacheKey, CacheKind.OpenApi, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or CliException or JsonException)
            {
                await Console.Error.WriteLineAsync($"Warning: dynamic commands could not be refreshed: {exception.Message}");
                return;
            }
        }

        if (cached is null)
        {
            return;
        }

        if (cached.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await Console.Error.WriteLineAsync("Warning: dynamic commands are using stale cached OpenAPI metadata.");
        }

        var manifestCache = await _cacheService.ReadAsync(cacheKey, CacheKind.Manifest, cancellationToken);
        if (manifestCache is not null)
        {
            var compatibility = CompatibilityService.Evaluate(CliUtilities.ParseManifest(manifestCache.Content));
            if (!compatibility.ProtocolCompatible || !compatibility.MinimumVersionSatisfied)
            {
                await Console.Error.WriteLineAsync("Warning: cached dynamic commands were skipped because the tenant management protocol is incompatible with this CLI.");
                return;
            }

            if (compatibility.ServerUsesNewerMinor)
            {
                await Console.Error.WriteLineAsync($"Warning: the tenant uses management protocol {compatibility.ManifestProtocolMajor}.{compatibility.ManifestProtocolMinor}; this CLI targets {compatibility.ExpectedProtocolMajor}.{compatibility.ExpectedProtocolMinor}.");
            }
        }

        var operations = OpenApiCliParser.Parse(cached.Content);
        foreach (var operation in operations)
        {
            AddDynamicCommand(operation);
        }

        AddDynamicSchemaCommands(operations);
    }

    private static Task WriteErrorAsync(string code, string message)
    {
        var error = new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

        return Console.Error.WriteLineAsync(error.ToJsonString());
    }

    private static Task WriteApiErrorAsync(ApiException exception)
    {
        var errorDetails = new JsonObject
        {
            ["code"] = "api_error",
            ["message"] = exception.Message,
            ["status"] = exception.StatusCode,
        };

        if (exception.Details is not null)
        {
            errorDetails["details"] = exception.Details.DeepClone();
        }

        if (!string.IsNullOrWhiteSpace(exception.CorrelationId))
        {
            errorDetails["correlationId"] = exception.CorrelationId;
        }

        return Console.Error.WriteLineAsync(new JsonObject { ["error"] = errorDetails }.ToJsonString());
    }

    private Command CreateLoginCommand()
    {
        var command = new Command("login", "Authenticate against the selected Orchard Core tenant");
        var contextArgument = new Argument<string?>("context") { Description = "Context name (defaults to the current context)" };
        contextArgument.DefaultValueFactory = _ => null;
        var grantOption = new Option<string?>("--grant") { Description = "Grant flow: browser, device, client-credentials", DefaultValueFactory = _ => "browser" };
        var clientIdOption = new Option<string?>("--client-id") { Description = "Client identifier for client credentials" };
        var clientSecretEnvOption = new Option<string?>("--client-secret-env") { Description = "Environment variable containing a client secret" };
        var clientSecretStdinOption = new Option<bool>("--client-secret-stdin") { Description = "Read the client secret from standard input" };

        command.Arguments.Add(contextArgument);
        command.Options.Add(grantOption);
        command.Options.Add(clientIdOption);
        command.Options.Add(clientSecretEnvOption);
        command.Options.Add(clientSecretStdinOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            var discovery = await _oauthClient.GetDiscoveryAsync(GetAuthority(context), cancellationToken);
            var grantType = parseResult.GetValue(grantOption)?.Trim().ToLowerInvariant() ?? "browser";
            EnsureGrantSupported(context, grantType);

            if (grantType == "client-credentials")
            {
                var clientId = parseResult.GetValue(clientIdOption) ?? throw new CliException("--client-id is required for client credentials.");
                var clientSecret = await ReadClientSecretAsync(parseResult.GetValue(clientSecretEnvOption), parseResult.GetValue(clientSecretStdinOption), cancellationToken);
                var token = await _oauthClient.ClientCredentialsAsync(
                    discovery.TokenEndpoint,
                    clientId,
                    clientSecret,
                    BuildClientCredentialsScope(context),
                    discovery.Issuer,
                    cancellationToken);

                return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new LoginOutput
                {
                    Context = context.Name,
                    GrantType = grantType,
                    ExpiresAt = token.ExpiresAt,
                    Issuer = token.Issuer,
                }, CliJsonContext.Default.LoginOutput), cancellationToken);
            }

            if (!_credentialStore.SupportsPersistentHumanTokens)
            {
                throw new CliException("Persistent human credentials require an OS-protected credential store. This platform is not supported; use per-command client credentials instead.");
            }

            var tokenSet = grantType switch
            {
                "browser" => await _oauthClient.LoginWithAuthorizationCodeAsync(context, discovery, cancellationToken),
                "device" => await _oauthClient.LoginWithDeviceCodeAsync(context, discovery, cancellationToken),
                _ => throw new CliException($"Unsupported grant type '{grantType}'.")
            };

            await _credentialStore.SaveAsync(context.Name, tokenSet, cancellationToken);
            _ = await RefreshManifestAsync(context, force: true, allowStale: false, cancellationToken);
            _ = await RefreshOpenApiAsync(context, force: true, allowStale: false, cancellationToken);

            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new LoginOutput
            {
                Context = context.Name,
                GrantType = grantType,
                ExpiresAt = tokenSet.ExpiresAt,
                Issuer = tokenSet.Issuer,
            }, CliJsonContext.Default.LoginOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateLogoutCommand()
    {
        var command = new Command("logout", "Remove stored human credentials for a context");
        var contextArgument = new Argument<string?>("context") { Description = "Context name (defaults to the current context)" };
        contextArgument.DefaultValueFactory = _ => null;
        command.Arguments.Add(contextArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            var token = await _credentialStore.GetAsync(context.Name, cancellationToken);
            var revoked = false;
            if (token is not null)
            {
                var discovery = await _oauthClient.GetDiscoveryAsync(GetAuthority(context), cancellationToken);
                if (!string.IsNullOrWhiteSpace(discovery.RevocationEndpoint))
                {
                    await _oauthClient.RevokeAsync(
                        discovery.RevocationEndpoint,
                        context.ClientId ?? throw new CliException("The selected context does not declare a CLI client identifier."),
                        token.RefreshToken ?? token.AccessToken,
                        token.RefreshToken is null ? "access_token" : "refresh_token",
                        cancellationToken);
                    revoked = true;
                }
            }

            var removed = await _credentialStore.DeleteAsync(context.Name, cancellationToken);
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new LogoutOutput
            {
                Context = context.Name,
                Removed = removed,
                Revoked = revoked,
            }, CliJsonContext.Default.LogoutOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateContextCommand()
    {
        var command = new Command("context", "Manage saved Orchard Core tenant contexts");
        command.Subcommands.Add(CreateContextListCommand());
        command.Subcommands.Add(CreateContextShowCommand());
        command.Subcommands.Add(CreateContextUseCommand());
        command.Subcommands.Add(CreateContextDeleteCommand());
        command.Subcommands.Add(CreateContextClearCommand());
        command.Subcommands.Add(CreateContextAddCommand());
        return command;
    }

    private Command CreateContextListCommand()
    {
        var command = new Command("list", "List saved contexts");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = new ContextListOutput
            {
                CurrentContext = _configuration.CurrentContext,
                Contexts = [],
            };

            foreach (var context in _configuration.Contexts.OrderBy(context => context.Name, StringComparer.OrdinalIgnoreCase))
            {
                output.Contexts.Add(new ContextOutput
                {
                    Name = context.Name,
                    TenantUrl = context.TenantUrl,
                    TenantId = context.TenantId,
                    ProductVersion = context.ProductVersion,
                    IsCurrent = string.Equals(context.Name, _configuration.CurrentContext, StringComparison.OrdinalIgnoreCase),
                    HasStoredCredentials = await _credentialStore.GetAsync(context.Name, cancellationToken) is not null,
                });
            }

            return await WriteOutputAsync(
                parseResult,
                CliUtilities.ToJsonElement(output, CliJsonContext.Default.ContextListOutput),
                cancellationToken,
                [
                    new("contexts[].name", "Name"),
                    new("contexts[].tenantUrl", "Tenant URL"),
                    new("contexts[].tenantId", "Tenant"),
                    new("contexts[].productVersion", "Version"),
                    new("contexts[].isCurrent", "Current"),
                    new("contexts[].hasStoredCredentials", "Authenticated"),
                ]);
        });

        return command;
    }

    private Command CreateContextShowCommand()
    {
        var command = new Command("show", "Show one saved context");
        var contextArgument = new Argument<string?>("context");
        contextArgument.DefaultValueFactory = _ => null;
        command.Arguments.Add(contextArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(await CreateContextOutputAsync(context, cancellationToken), CliJsonContext.Default.ContextOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateContextUseCommand()
    {
        var command = new Command("use", "Select the current context");
        var nameArgument = new Argument<string>("name");
        command.Arguments.Add(nameArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(nameArgument));
            _configuration.CurrentContext = context.Name;
            await _contextStore.SaveAsync(_configuration, cancellationToken);
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(await CreateContextOutputAsync(context, cancellationToken), CliJsonContext.Default.ContextOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateContextDeleteCommand()
    {
        var command = new Command("delete", "Delete a saved context");
        var nameArgument = new Argument<string>("name");
        var yesOption = new Option<bool>("--yes") { Description = "Confirm deletion without a prompt" };
        command.Arguments.Add(nameArgument);
        command.Options.Add(yesOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            if (!parseResult.GetValue(yesOption))
            {
                throw new CliException("Context deletion is destructive. Re-run with --yes to confirm.");
            }

            var name = parseResult.GetValue(nameArgument) ?? throw new CliException("A context name is required.");
            if (!ContextStore.Delete(_configuration, name))
            {
                throw new CliException($"Context '{name}' was not found.");
            }

            await _credentialStore.DeleteAsync(name, cancellationToken);
            await _contextStore.SaveAsync(_configuration, cancellationToken);
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new ContextOutput
            {
                Name = name,
                TenantUrl = string.Empty,
                IsCurrent = false,
                HasStoredCredentials = false,
            }, CliJsonContext.Default.ContextOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateContextClearCommand()
    {
        var command = new Command("clear", "Delete all saved contexts and their stored credentials");
        var forceOption = new Option<bool>("--force") { Description = "Delete without prompting for confirmation" };
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var contexts = _configuration.Contexts.ToArray();
            if (contexts.Length > 0 &&
                !parseResult.GetValue(forceOption) &&
                !await ConfirmContextClearAsync(contexts.Length, Console.In, Console.Error, cancellationToken))
            {
                return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new ContextClearOutput
                {
                    Cleared = false,
                }, CliJsonContext.Default.ContextClearOutput), cancellationToken);
            }

            var deletedCredentials = 0;
            foreach (var context in contexts)
            {
                if (await _credentialStore.DeleteAsync(context.Name, cancellationToken))
                {
                    deletedCredentials++;
                }
            }

            _configuration.Contexts.Clear();
            _configuration.CurrentContext = null;
            await _contextStore.SaveAsync(_configuration, cancellationToken);

            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new ContextClearOutput
            {
                Cleared = true,
                DeletedContexts = contexts.Length,
                DeletedCredentials = deletedCredentials,
            }, CliJsonContext.Default.ContextClearOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateContextAddCommand()
    {
        var command = new Command("add", "Add or update a tenant context from its exact tenant URL");
        var nameArgument = new Argument<string>("name");
        var urlArgument = new Argument<string>("url");
        var currentOption = new Option<bool>("--current") { Description = "Make the added context current" };
        command.Arguments.Add(nameArgument);
        command.Arguments.Add(urlArgument);
        command.Options.Add(currentOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument) ?? throw new CliException("A context name is required.");
            var url = CliPaths.NormalizeTenantUrl(parseResult.GetValue(urlArgument) ?? throw new CliException("A tenant URL is required."));
            var manifest = await FetchBootstrapAsync(url, cancellationToken);
            var context = ContextStore.AddOrUpdate(_configuration, name, manifest, parseResult.GetValue(currentOption));
            await _contextStore.SaveAsync(_configuration, cancellationToken);
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(await CreateContextOutputAsync(context, cancellationToken), CliJsonContext.Default.ContextOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateApiCommand()
    {
        var command = new Command("api", "Manage API metadata and invoke remote operations");
        command.Subcommands.Add(CreateApiRefreshCommand());
        command.Subcommands.Add(CreateApiDescribeCommand());
        command.Subcommands.Add(CreateApiInvokeCommand());
        command.Subcommands.Add(CreateApiCompatibilityCommand());
        return command;
    }

    private Command CreateApiRefreshCommand()
    {
        var command = new Command("refresh", "Refresh the management manifest and OpenAPI cache");
        var contextArgument = new Argument<string?>("context");
        contextArgument.DefaultValueFactory = _ => null;
        var forceOption = new Option<bool>("--force") { Description = "Bypass TTL checks and force a conditional refresh" };
        command.Arguments.Add(contextArgument);
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            var force = parseResult.GetValue(forceOption);
            var manifest = await RefreshManifestAsync(context, force, allowStale: false, cancellationToken);
            var openApi = await RefreshOpenApiAsync(context, force, allowStale: false, cancellationToken);
            var compatibility = CompatibilityService.Evaluate(CliUtilities.ParseManifest(manifest.CacheRecord.Content));
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new RefreshOutput
            {
                Context = context.Name,
                ManifestFromCache = manifest.FromCache,
                OpenApiFromCache = openApi.FromCache,
                Compatibility = compatibility,
            }, CliJsonContext.Default.RefreshOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateApiDescribeCommand()
    {
        var command = new Command("describe", "Show the cached or refreshed management manifest");
        var contextArgument = new Argument<string?>("context");
        contextArgument.DefaultValueFactory = _ => null;
        command.Arguments.Add(contextArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            var manifest = await RefreshManifestAsync(context, force: false, allowStale: false, cancellationToken);
            using var document = JsonDocument.Parse(manifest.CacheRecord.Content);
            return await WriteOutputAsync(parseResult, document.RootElement.Clone(), cancellationToken);
        });

        return command;
    }

    private Command CreateApiCompatibilityCommand()
    {
        var command = new Command("compatibility", "Evaluate CLI compatibility with the selected tenant");
        var contextArgument = new Argument<string?>("context");
        contextArgument.DefaultValueFactory = _ => null;
        command.Arguments.Add(contextArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(contextArgument) ?? parseResult.GetValue(_contextOption));
            var manifest = await RefreshManifestAsync(context, force: false, allowStale: false, cancellationToken);
            var compatibility = CompatibilityService.Evaluate(CliUtilities.ParseManifest(manifest.CacheRecord.Content));
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(compatibility, CliJsonContext.Default.CompatibilityOutput), cancellationToken);
        });

        return command;
    }

    private Command CreateApiInvokeCommand()
    {
        var command = new Command("invoke", "Invoke a remote API path directly");
        var methodArgument = new Argument<string>("method");
        var pathArgument = new Argument<string>("path");
        var queryOption = new Option<string[]>("--query") { Description = "Query parameter in key=value form", AllowMultipleArgumentsPerToken = true };
        var headerOption = new Option<string[]>("--header") { Description = "Header in key=value form", AllowMultipleArgumentsPerToken = true };
        var bodyOption = new Option<string?>("--body") { Description = "Inline JSON request body" };
        var bodyFileOption = new Option<FileInfo?>("--body-file") { Description = "Read the JSON request body from a file" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from standard input" };
        var contentTypeOption = new Option<string?>("--content-type") { Description = "Explicit request content type", DefaultValueFactory = _ => "application/json" };
        var clientIdOption = new Option<string?>("--client-id") { Description = "Client identifier for client credentials" };
        var clientSecretEnvOption = new Option<string?>("--client-secret-env") { Description = "Environment variable containing a client secret" };
        var clientSecretStdinOption = new Option<bool>("--client-secret-stdin") { Description = "Read the client secret from standard input" };
        command.Arguments.Add(methodArgument);
        command.Arguments.Add(pathArgument);
        command.Options.Add(queryOption);
        command.Options.Add(headerOption);
        command.Options.Add(bodyOption);
        command.Options.Add(bodyFileOption);
        command.Options.Add(stdinOption);
        command.Options.Add(contentTypeOption);
        command.Options.Add(clientIdOption);
        command.Options.Add(clientSecretEnvOption);
        command.Options.Add(clientSecretStdinOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = RequireContext(parseResult.GetValue(_contextOption));
            var body = await ResolveJsonBodyAsync(parseResult.GetValue(bodyOption), parseResult.GetValue(bodyFileOption), parseResult.GetValue(stdinOption), parseResult.GetValue(contentTypeOption) ?? "application/json", cancellationToken);
            var accessToken = await ResolveAccessTokenAsync(context, parseResult.GetValue(clientIdOption), parseResult.GetValue(clientSecretEnvOption), parseResult.GetValue(clientSecretStdinOption), cancellationToken);
            var response = await SendApiRequestAsync(
                context,
                parseResult.GetValue(methodArgument) ?? throw new CliException("An HTTP method is required."),
                parseResult.GetValue(pathArgument) ?? throw new CliException("A request path is required."),
                ParseKeyValuePairs(parseResult.GetValue(queryOption)),
                ParseKeyValuePairs(parseResult.GetValue(headerOption)),
                body,
                accessToken,
                cancellationToken);
            return await WriteOutputAsync(parseResult, response.Json, cancellationToken, response.TableColumns);
        });

        return command;
    }

    private Command CreateDocsCommand()
    {
        var command = new Command("docs", "Work with cached Orchard Core documentation search indexes");
        command.Subcommands.Add(CreateDocsUpdateCommand());
        command.Subcommands.Add(CreateDocsSearchCommand());
        command.Subcommands.Add(CreateDocsShowCommand());
        return command;
    }

    private Command CreateDocsUpdateCommand()
    {
        var command = new Command("update", "Refresh the cached Orchard Core documentation index");
        var forceOption = new Option<bool>("--force") { Description = "Bypass TTL checks and force a conditional refresh" };
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await RefreshDocumentationAsync(parseResult.GetValue(forceOption), allowStale: true, cancellationToken);
            var output = new JsonObject
            {
                ["url"] = DocumentationIndexUri.AbsoluteUri,
                ["fromCache"] = result.FromCache,
                ["stale"] = result.IsStale,
            };
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(output), cancellationToken);
        });

        return command;
    }

    private Command CreateDocsSearchCommand()
    {
        var command = new Command("search", "Search the cached MkDocs search index");
        var queryArgument = new Argument<string>("query");
        command.Arguments.Add(queryArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var documentation = await RefreshDocumentationAsync(force: false, allowStale: true, cancellationToken);
            var output = DocumentationService.Search(CliUtilities.ParseDocumentationIndex(documentation.CacheRecord.Content), GetDocumentationBaseUri(), parseResult.GetValue(queryArgument) ?? throw new CliException("A query is required."));
            return await WriteOutputAsync(
                parseResult,
                CliUtilities.ToJsonElement(output, CliJsonContext.Default.DocumentationSearchOutput),
                cancellationToken,
                [
                    new("results[].title", "Title"),
                    new("results[].location", "Location"),
                    new("results[].score", "Score"),
                ]);
        });

        return command;
    }

    private Command CreateDocsShowCommand()
    {
        var command = new Command("show", "Show a cached documentation entry by id, title, or location");
        var selectorArgument = new Argument<string>("selector");
        command.Arguments.Add(selectorArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var documentation = await RefreshDocumentationAsync(force: false, allowStale: true, cancellationToken);
            var output = DocumentationService.Show(CliUtilities.ParseDocumentationIndex(documentation.CacheRecord.Content), GetDocumentationBaseUri(), parseResult.GetValue(selectorArgument) ?? throw new CliException("A selector is required."));
            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(output, CliJsonContext.Default.DocumentationShowOutput), cancellationToken);
        });

        return command;
    }

    private static Command CreateCompletionCommand()
    {
        var command = new Command("completion", "Print basic completion instructions");
        var shellOption = new Option<string?>("--shell") { Description = "Shell: bash, zsh, fish, pwsh", DefaultValueFactory = _ => "bash" };
        command.Options.Add(shellOption);
        command.SetAction(parseResult =>
        {
            var shell = parseResult.GetValue(shellOption)?.Trim().ToLowerInvariant() ?? "bash";
            var text = shell switch
            {
                "bash" => "Install dotnet-suggest, then run: dotnet suggest script bash | source /dev/stdin && dotnet-suggest register --command-path oc",
                "zsh" => "Install dotnet-suggest, then run: dotnet suggest script zsh | source /dev/stdin && dotnet-suggest register --command-path oc",
                "fish" => "Install dotnet-suggest, then run: dotnet suggest script fish | source && dotnet-suggest register --command-path oc",
                "pwsh" => "Install dotnet-suggest, then run: dotnet suggest script powershell | Invoke-Expression; dotnet-suggest register --command-path oc",
                _ => throw new CliException($"Unsupported shell '{shell}'.")
            };

            Console.Out.WriteLine(text);
            return 0;
        });
        return command;
    }

    private Command CreateDoctorCommand()
    {
        var command = new Command("doctor", "Inspect CLI environment, cache, and secure storage availability");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var currentContext = ContextStore.FindContext(_configuration, parseResult.GetValue(_contextOption));
            var output = new DoctorOutput
            {
                RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                OperatingSystem = CliUtilities.DetermineOperatingSystem(),
                ConfigDirectory = _paths.RootDirectory,
                CacheDirectory = _paths.CacheDirectory,
                CredentialStore = _credentialStore.DisplayName,
                CurrentContext = currentContext?.Name,
                HasManifestCache = currentContext is not null && await _cacheService.ReadAsync(currentContext.TenantUrl, CacheKind.Manifest, cancellationToken) is not null,
                HasOpenApiCache = currentContext is not null && await _cacheService.ReadAsync(currentContext.TenantUrl, CacheKind.OpenApi, cancellationToken) is not null,
                HasDocumentationCache = await _cacheService.ReadAsync(DocumentationIndexUri.AbsoluteUri, CacheKind.Documentation, cancellationToken) is not null,
            };

            return await WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(output, CliJsonContext.Default.DoctorOutput), cancellationToken);
        });
        return command;
    }

    private Command CreateVersionCommand()
    {
        var command = new Command("version", "Show CLI version information");
        command.SetAction((parseResult, cancellationToken) => WriteOutputAsync(parseResult, CliUtilities.ToJsonElement(new VersionOutput
        {
            CliVersion = CliUtilities.CliVersion,
            RuntimeVersion = global::System.Environment.Version.ToString(),
            RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
        }, CliJsonContext.Default.VersionOutput), cancellationToken));
        return command;
    }

    private void AddDynamicCommand(OpenApiOperationDefinition operation)
    {
        var command = EnsureCommandPath(operation.CliMetadata.CommandGroup, operation.CliMetadata.Verb, operation.Summary ?? operation.Description ?? operation.OperationId ?? $"{operation.Method} {operation.Path}");
        if (command is null)
        {
            Console.Error.WriteLine($"Warning: skipped duplicate dynamic command '{string.Join(' ', operation.CliMetadata.CommandGroup)} {operation.CliMetadata.Verb}'.");
            return;
        }

        command.Hidden = operation.CliMetadata.Hidden;
        foreach (var alias in operation.CliMetadata.Aliases)
        {
            command.Aliases.Add(alias);
        }

        var positionalArguments = new List<(OpenApiParameterDefinition Parameter, Argument<string> Argument)>();
        var options = new List<(OpenApiParameterDefinition Parameter, Option<string?> Option)>();
        var bodyPropertyOptions = new List<(RequestBodyPropertyDefinition Property, Option<string?> Option)>();

        foreach (var parameter in operation.Parameters.OrderBy(parameter => parameter.ArgumentPosition ?? int.MaxValue).ThenBy(parameter => parameter.Name, StringComparer.Ordinal))
        {
            if (parameter.ArgumentPosition.HasValue)
            {
                var argument = new Argument<string>(CliUtilities.ToCliName(parameter.Name)) { Description = parameter.Description };
                command.Arguments.Add(argument);
                positionalArguments.Add((parameter, argument));
            }
            else
            {
                var option = new Option<string?>($"--{CliUtilities.ToCliName(parameter.Name)}") { Description = parameter.Description, Required = parameter.Required };
                command.Options.Add(option);
                options.Add((parameter, option));
            }
        }

        var yesOption = new Option<bool>("--yes") { Description = "Confirm destructive operations without a prompt" };
        var bodyOption = new Option<string?>("--body") { Description = "Inline JSON request body" };
        var bodyFileOption = new Option<FileInfo?>("--body-file") { Description = "Read the JSON request body from a file" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from standard input" };
        var fileOption = new Option<FileInfo?>("--file") { Description = "Read the binary request body from a file" };

        if (operation.CliMetadata.RequiresConfirmation)
        {
            command.Options.Add(yesOption);
        }

        if (operation.HasJsonRequestBody)
        {
            command.Options.Add(bodyOption);
            command.Options.Add(bodyFileOption);
            command.Options.Add(stdinOption);
        }

        if (operation.HasBinaryRequestBody)
        {
            command.Options.Add(fileOption);
            command.Options.Add(stdinOption);
        }

        if (operation.CliMetadata.InputMode == CliInputMode.Options)
        {
            foreach (var property in operation.RequestBodyProperties)
            {
                var option = new Option<string?>($"--{CliUtilities.ToCliName(property.Name)}") { Description = property.Description, Required = property.Required };
                command.Options.Add(option);
                bodyPropertyOptions.Add((property, option));
            }
        }

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            if (operation.CliMetadata.RequiresConfirmation && !parseResult.GetValue(yesOption))
            {
                throw new CliException("This operation is marked destructive. Re-run with --yes to confirm.");
            }

            var context = RequireContext(parseResult.GetValue(_contextOption));
            var routePath = operation.Path;
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (parameter, argument) in positionalArguments)
            {
                var value = parseResult.GetValue(argument) ?? throw new CliException($"Argument {CliUtilities.ToCliName(parameter.Name)} is required.");
                if (string.Equals(parameter.Location, "path", StringComparison.OrdinalIgnoreCase))
                {
                    routePath = routePath.Replace($"{{{parameter.Name}}}", Uri.EscapeDataString(value), StringComparison.Ordinal);
                }
                else if (string.Equals(parameter.Location, "query", StringComparison.OrdinalIgnoreCase))
                {
                    query[parameter.Name] = value;
                }
                else if (string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase))
                {
                    headers[parameter.Name] = value;
                }
            }

            foreach (var (parameter, option) in options)
            {
                var value = parseResult.GetValue(option);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (string.Equals(parameter.Location, "query", StringComparison.OrdinalIgnoreCase))
                {
                    query[parameter.Name] = value;
                }
                else if (string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase))
                {
                    headers[parameter.Name] = value;
                }
            }

            HttpContent? body = operation.HasBinaryRequestBody
                ? await ResolveBinaryBodyAsync(parseResult.GetValue(fileOption), parseResult.GetValue(stdinOption), cancellationToken)
                : await ResolveDynamicJsonBodyAsync(parseResult, operation, bodyOption, bodyFileOption, stdinOption, bodyPropertyOptions, cancellationToken);

            var accessToken = await ResolveAccessTokenAsync(context, null, null, false, cancellationToken);
            var response = await SendApiRequestAsync(context, operation.Method, routePath, query, headers, body, accessToken, cancellationToken);
            IReadOnlyList<CliTableColumnMetadata>? tableColumns = operation.CliMetadata.TableColumns.Count > 0 ? [.. operation.CliMetadata.TableColumns] : null;
            return await WriteOutputAsync(parseResult, response.Json, cancellationToken, tableColumns);
        });
    }

    private Command? EnsureCommandPath(IReadOnlyList<string> commandGroup, string verb, string description)
    {
        Command current = _rootCommand;
        foreach (var segment in commandGroup)
        {
            var next = current.Subcommands.FirstOrDefault(command => string.Equals(command.Name, segment, StringComparison.Ordinal));
            if (next is null)
            {
                next = new Command(segment);
                current.Subcommands.Add(next);
            }

            current = next;
        }

        var existing = current.Subcommands.FirstOrDefault(command => string.Equals(command.Name, verb, StringComparison.Ordinal));
        if (existing is not null)
        {
            return null;
        }

        var created = new Command(verb, description);
        current.Subcommands.Add(created);
        return created;
    }

    private void AddDynamicSchemaCommands(IReadOnlyList<OpenApiOperationDefinition> operations)
    {
        foreach (var group in operations
            .Where(operation => operation.RequestBodySchema is not null)
            .GroupBy(operation => string.Join('\0', operation.CliMetadata.CommandGroup), StringComparer.Ordinal))
        {
            var inputOperations = group.ToArray();
            var commandGroup = inputOperations[0].CliMetadata.CommandGroup;
            var command = EnsureCommandPath(commandGroup, "schema", "Shows the JSON Schema accepted by this resource.");
            if (command is null)
            {
                continue;
            }

            var operationOption = new Option<string?>("--operation")
            {
                Description = $"Input operation verb when this resource accepts multiple request shapes: {FormatOperationList(inputOperations)}",
            };
            command.Options.Add(operationOption);

            command.SetAction((parseResult, cancellationToken) =>
            {
                var requestedOperation = parseResult.GetValue(operationOption);
                var selected = SelectSchemaOperation(inputOperations, requestedOperation);

                return WriteOutputAsync(
                    parseResult,
                    CliUtilities.ToJsonElement(selected.RequestBodySchema!),
                    cancellationToken);
            });
        }
    }

    internal static OpenApiOperationDefinition SelectSchemaOperation(
        IReadOnlyList<OpenApiOperationDefinition> operations,
        string? requestedOperation)
    {
        ArgumentNullException.ThrowIfNull(operations);

        if (operations.Count == 0)
        {
            throw new ArgumentException("At least one input operation is required.", nameof(operations));
        }

        if (!string.IsNullOrWhiteSpace(requestedOperation))
        {
            return operations.FirstOrDefault(operation =>
                string.Equals(operation.CliMetadata.Verb, requestedOperation, StringComparison.OrdinalIgnoreCase)
                || operation.CliMetadata.Aliases.Contains(requestedOperation, StringComparer.OrdinalIgnoreCase))
                ?? throw new CliException($"Unknown input operation '{requestedOperation}'. Available operations: {FormatOperationList(operations)}.");
        }

        var selected = operations[0];
        if (operations.Skip(1).Any(operation => !JsonNode.DeepEquals(selected.RequestBodySchema, operation.RequestBodySchema)))
        {
            throw new CliException($"This resource accepts multiple input schemas. Specify --operation with one of: {FormatOperationList(operations)}.");
        }

        return selected;
    }

    private static string FormatOperationList(IEnumerable<OpenApiOperationDefinition> operations) =>
        string.Join(", ", operations.Select(operation => operation.CliMetadata.Verb).Order());

    private async Task<CachedContentResult> RefreshManifestAsync(TenantContextRecord context, bool force, bool allowStale, CancellationToken cancellationToken)
    {
        var manifestUri = GetManagementManifestUri(context);
        var accessToken = await ResolveAccessTokenAsync(context, null, null, false, cancellationToken);
        var result = await _cacheService.GetOrRefreshAsync(
            context.TenantUrl,
            CacheKind.Manifest,
            manifestUri,
            ManifestTtl,
            force,
            async (eTag, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
                CacheService.AddIfNoneMatchHeader(request, eTag);
                AddAuthorization(request, accessToken);
                return await _httpClient.SendAsync(request, token);
            },
            cancellationToken);

        if (!allowStale && result.IsStale)
        {
            throw new CliException("Unable to refresh the management manifest and no fresh cache entry is available.");
        }

        var manifest = CliUtilities.ParseManifest(result.CacheRecord.Content);
        _ = ContextStore.AddOrUpdate(_configuration, context.Name, manifest, string.Equals(_configuration.CurrentContext, context.Name, StringComparison.OrdinalIgnoreCase));
        await _contextStore.SaveAsync(_configuration, cancellationToken);
        return result;
    }

    private async Task<CachedContentResult> RefreshOpenApiAsync(TenantContextRecord context, bool force, bool allowStale, CancellationToken cancellationToken)
    {
        var manifestCache = await RefreshManifestAsync(context, force: false, allowStale: false, cancellationToken);
        var manifest = CliUtilities.ParseManifest(manifestCache.CacheRecord.Content);
        EnsureCompatible(manifest);
        var openApiUri = manifest.OpenApiUrl ?? (!string.IsNullOrWhiteSpace(context.OpenApiUrl) ? new Uri(context.OpenApiUrl, UriKind.Absolute) : null)
            ?? throw new CliException("The selected context does not expose an OpenAPI endpoint.");
        var accessToken = await ResolveAccessTokenAsync(context, null, null, false, cancellationToken);

        var result = await _cacheService.GetOrRefreshAsync(
            context.TenantUrl,
            CacheKind.OpenApi,
            openApiUri,
            OpenApiTtl,
            force,
            async (eTag, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, openApiUri);
                CacheService.AddIfNoneMatchHeader(request, eTag ?? manifest.OpenApiETag);
                AddAuthorization(request, accessToken);
                return await _httpClient.SendAsync(request, token);
            },
            cancellationToken);

        if (!allowStale && result.IsStale)
        {
            throw new CliException("Unable to refresh the OpenAPI document and no fresh cache entry is available.");
        }

        return result;
    }

    private async Task<CachedContentResult> RefreshDocumentationAsync(bool force, bool allowStale, CancellationToken cancellationToken)
    {
        var result = await _cacheService.GetOrRefreshAsync(
            DocumentationIndexUri.AbsoluteUri,
            CacheKind.Documentation,
            DocumentationIndexUri,
            DocumentationTtl,
            force,
            async (eTag, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, DocumentationIndexUri);
                CacheService.AddIfNoneMatchHeader(request, eTag);
                return await _httpClient.SendAsync(request, token);
            },
            cancellationToken);

        if (!allowStale && result.IsStale)
        {
            throw new CliException("Unable to refresh the documentation index and no fresh cache entry is available.");
        }

        return result;
    }

    private async Task<CommandOutput> SendApiRequestAsync(
        TenantContextRecord context,
        string method,
        string path,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> headers,
        HttpContent? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), BuildRequestUri(new Uri(context.TenantUrl, UriKind.Absolute), path, query))
        {
            Content = body,
        };

        AddAuthorization(request, accessToken);
        foreach (var header in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                _ = request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var payload = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            JsonNode? details = null;
            if (!string.IsNullOrWhiteSpace(payload) &&
                (string.Equals(contentType, "application/problem+json", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                 payload.StartsWith('{') ||
                 payload.StartsWith('[')))
            {
                try
                {
                    details = JsonNode.Parse(payload);
                }
                catch (JsonException)
                {
                    details = JsonValue.Create(payload);
                }
            }

            throw new ApiException(
                (int)response.StatusCode,
                $"API request {request.Method} {request.RequestUri?.PathAndQuery} failed with {status}.",
                details,
                GetCorrelationId(response));
        }

        if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            || payload.StartsWith('{')
            || payload.StartsWith('['))
        {
            using var document = JsonDocument.Parse(payload);
            return new CommandOutput { Json = document.RootElement.Clone() };
        }

        var node = new JsonObject
        {
            ["statusCode"] = (int)response.StatusCode,
            ["contentType"] = contentType,
            ["body"] = payload,
        };

        return new CommandOutput { Json = CliUtilities.ToJsonElement(node) };
    }

    private static string? GetCorrelationId(HttpResponseMessage response)
    {
        foreach (var headerName in new[] { "X-Correlation-ID", "Correlation-Id", "Request-Id", "traceparent" })
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    private async Task<string?> ResolveAccessTokenAsync(TenantContextRecord context, string? clientId, string? clientSecretEnv, bool clientSecretStdin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = global::System.Environment.GetEnvironmentVariable("OC_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                clientSecretEnv = "OC_CLIENT_SECRET";
            }
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var discovery = await _oauthClient.GetDiscoveryAsync(GetAuthority(context), cancellationToken);
            var secret = await ReadClientSecretAsync(clientSecretEnv, clientSecretStdin, cancellationToken);
            var token = await _oauthClient.ClientCredentialsAsync(discovery.TokenEndpoint, clientId, secret, BuildClientCredentialsScope(context), discovery.Issuer, cancellationToken);
            return token.AccessToken;
        }

        var stored = await _credentialStore.GetAsync(context.Name, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var discoveryDocument = await _oauthClient.GetDiscoveryAsync(GetAuthority(context), cancellationToken);
        CliUtilities.EnsureIssuerMatches(discoveryDocument.Issuer, stored.Issuer);
        if (stored.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            stored = await _oauthClient.RefreshAsync(context, discoveryDocument, stored, cancellationToken);
            await _credentialStore.SaveAsync(context.Name, stored, cancellationToken);
        }

        return stored.AccessToken;
    }

    private static async Task<HttpContent?> ResolveDynamicJsonBodyAsync(
        ParseResult parseResult,
        OpenApiOperationDefinition operation,
        Option<string?> bodyOption,
        Option<FileInfo?> bodyFileOption,
        Option<bool> stdinOption,
        List<(RequestBodyPropertyDefinition Property, Option<string?> Option)> bodyPropertyOptions,
        CancellationToken cancellationToken)
    {
        var explicitBody = await ResolveJsonBodyAsync(parseResult.GetValue(bodyOption), parseResult.GetValue(bodyFileOption), parseResult.GetValue(stdinOption), operation.RequestContentType ?? "application/json", cancellationToken);
        if (explicitBody is not null)
        {
            ValidateDynamicJsonBody(await explicitBody.ReadAsStringAsync(cancellationToken), operation);
            return explicitBody;
        }

        if (!string.IsNullOrWhiteSpace(operation.CliMetadata.DefaultJsonBody))
        {
            ValidateDynamicJsonBody(operation.CliMetadata.DefaultJsonBody, operation);
            return CreateJsonContent(operation.CliMetadata.DefaultJsonBody, operation.RequestContentType ?? "application/json");
        }

        if (bodyPropertyOptions.Count == 0)
        {
            if (operation.RequestBodyRequired)
            {
                throw new CliException(CreateRequiredBodyMessage(operation, includeInputOptions: true));
            }

            return null;
        }

        var node = new JsonObject();
        foreach (var (property, option) in bodyPropertyOptions)
        {
            var value = parseResult.GetValue(option);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (property.Required)
                {
                    throw new CliException($"--{CliUtilities.ToCliName(property.Name)} is required.");
                }

                continue;
            }

            node[property.Name] = CliUtilities.ConvertToJsonNode(value, property.Type ?? "string");
        }

        if (node.Count == 0)
        {
            if (operation.RequestBodyRequired)
            {
                throw new CliException(CreateRequiredBodyMessage(operation, includeInputOptions: false));
            }

            return null;
        }

        var content = node.ToJsonString();
        ValidateDynamicJsonBody(content, operation);
        return CreateJsonContent(content, operation.RequestContentType ?? "application/json");
    }

    internal static string CreateRequiredBodyMessage(OpenApiOperationDefinition operation, bool includeInputOptions)
    {
        var schemaCommand = $"oc {string.Join(' ', operation.CliMetadata.CommandGroup)} schema --operation {operation.CliMetadata.Verb}";
        var inputGuidance = includeInputOptions
            ? " Provide --body, --body-file, or --stdin."
            : string.Empty;

        return $"A JSON request body is required.{inputGuidance} Run '{schemaCommand}' to inspect its schema.";
    }

    private static void ValidateDynamicJsonBody(string content, OpenApiOperationDefinition operation)
    {
        using var document = JsonDocument.Parse(content);
        if (operation.RequestBodyProperties.Count == 0)
        {
            return;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new CliException("The request body must be a JSON object.");
        }

        foreach (var property in operation.RequestBodyProperties)
        {
            if (!document.RootElement.TryGetProperty(property.Name, out var value))
            {
                if (property.Required)
                {
                    throw new CliException($"The request body property '{property.Name}' is required.");
                }

                continue;
            }

            if (property.Type is not null && !MatchesSchemaType(value, property.Type))
            {
                throw new CliException($"The request body property '{property.Name}' must be of type '{property.Type}'.");
            }

            if (property.AllowedValues.Count > 0 && !property.AllowedValues.Contains(value.GetRawText(), StringComparer.Ordinal))
            {
                throw new CliException($"The request body property '{property.Name}' must be one of: {string.Join(", ", property.AllowedValues)}.");
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var length = value.GetString()?.Length ?? 0;
                if (property.MinimumLength.HasValue && length < property.MinimumLength)
                {
                    throw new CliException($"The request body property '{property.Name}' must contain at least {property.MinimumLength} characters.");
                }

                if (property.MaximumLength.HasValue && length > property.MaximumLength)
                {
                    throw new CliException($"The request body property '{property.Name}' must contain at most {property.MaximumLength} characters.");
                }
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                if (property.Minimum.HasValue && number < property.Minimum)
                {
                    throw new CliException($"The request body property '{property.Name}' must be at least {property.Minimum}.");
                }

                if (property.Maximum.HasValue && number > property.Maximum)
                {
                    throw new CliException($"The request body property '{property.Name}' must be at most {property.Maximum}.");
                }
            }
        }
    }

    private static bool MatchesSchemaType(JsonElement value, string type) => type switch
    {
        "array" => value.ValueKind == JsonValueKind.Array,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "object" => value.ValueKind == JsonValueKind.Object,
        "string" => value.ValueKind == JsonValueKind.String,
        _ => true,
    };

    private static async Task<HttpContent?> ResolveJsonBodyAsync(string? inlineBody, FileInfo? bodyFile, bool stdin, string contentType, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(inlineBody))
        {
            return CreateJsonContent(inlineBody, contentType);
        }

        if (bodyFile is not null)
        {
            return CreateJsonContent(await File.ReadAllTextAsync(bodyFile.FullName, cancellationToken), contentType);
        }

        if (stdin)
        {
            return CreateJsonContent(await Console.In.ReadToEndAsync(cancellationToken), contentType);
        }

        return null;
    }

    private static Task<HttpContent?> ResolveBinaryBodyAsync(FileInfo? file, bool stdin, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (file is not null)
        {
            return Task.FromResult<HttpContent?>(CreateBinaryContent(File.OpenRead(file.FullName)));
        }

        if (!stdin)
        {
            return Task.FromResult<HttpContent?>(null);
        }

        return Task.FromResult<HttpContent?>(CreateBinaryContent(Console.OpenStandardInput()));
    }

    private static StreamContent CreateBinaryContent(Stream stream)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static StringContent CreateJsonContent(string content, string contentType)
    {
        _ = JsonDocument.Parse(content);
        return new StringContent(content, Encoding.UTF8, contentType);
    }

    private async Task<ContextOutput> CreateContextOutputAsync(TenantContextRecord context, CancellationToken cancellationToken) => new()
    {
        Name = context.Name,
        TenantUrl = context.TenantUrl,
        TenantId = context.TenantId,
        ProductVersion = context.ProductVersion,
        IsCurrent = string.Equals(context.Name, _configuration.CurrentContext, StringComparison.OrdinalIgnoreCase),
        HasStoredCredentials = await _credentialStore.GetAsync(context.Name, cancellationToken) is not null,
    };

    private static string BuildClientCredentialsScope(TenantContextRecord context)
    {
        var scopes = context.Scopes.Count > 0
            ? context.Scopes.Where(scope =>
                !string.Equals(scope, "openid", StringComparison.Ordinal) &&
                !string.Equals(scope, "profile", StringComparison.Ordinal) &&
                !string.Equals(scope, "email", StringComparison.Ordinal) &&
                !string.Equals(scope, "offline_access", StringComparison.Ordinal))
            : [RemoteManagementConstants.ManagementScope];

        return string.Join(' ', scopes);
    }

    private static void EnsureCompatible(RemoteManagementManifest manifest)
    {
        var compatibility = CompatibilityService.Evaluate(manifest);
        if (!compatibility.ProtocolCompatible)
        {
            throw new CliException(
                $"The tenant uses incompatible management protocol {compatibility.ManifestProtocolMajor}.{compatibility.ManifestProtocolMinor}; " +
                $"this CLI supports {compatibility.ExpectedProtocolMajor}.x.");
        }

        if (!compatibility.MinimumVersionSatisfied)
        {
            throw new CliException($"The tenant requires CLI version {compatibility.MinimumCliVersion} or later.");
        }

        if (compatibility.ServerUsesNewerMinor)
        {
            Console.Error.WriteLine(
                $"Warning: the tenant uses management protocol {compatibility.ManifestProtocolMajor}.{compatibility.ManifestProtocolMinor}; " +
                $"this CLI targets {compatibility.ExpectedProtocolMajor}.{compatibility.ExpectedProtocolMinor}.");
        }
    }

    private static void EnsureGrantSupported(TenantContextRecord context, string grantType)
    {
        var protocolGrant = grantType switch
        {
            "browser" => "authorization_code",
            "device" => "urn:ietf:params:oauth:grant-type:device_code",
            "client-credentials" => "client_credentials",
            _ => throw new CliException($"Unsupported grant type '{grantType}'."),
        };

        if (!context.GrantTypes.Contains(protocolGrant, StringComparer.Ordinal))
        {
            throw new CliException(
                $"The tenant does not advertise the '{grantType}' login flow. Configure the corresponding OpenID server grant and endpoint settings.");
        }
    }

    private static void AddAuthorization(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    internal static async Task<bool> ConfirmContextClearAsync(
        int contextCount,
        TextReader input,
        TextWriter promptWriter,
        CancellationToken cancellationToken)
    {
        await promptWriter.WriteAsync($"Delete all {contextCount} saved context{(contextCount == 1 ? string.Empty : "s")} and stored credentials? [y/N] ");
        await promptWriter.FlushAsync(cancellationToken);
        var response = await input.ReadLineAsync(cancellationToken);

        return string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri BuildRequestUri(Uri baseUri, string path, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(new Uri(baseUri, path.TrimStart('/')));
        if (query.Count > 0)
        {
            builder.Query = string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        return builder.Uri;
    }

    private static Dictionary<string, string> ParseKeyValuePairs(IEnumerable<string>? values)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (values is null)
        {
            return dictionary;
        }

        foreach (var value in values)
        {
            var separatorIndex = value.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new CliException($"Expected key=value but received '{value}'.");
            }

            dictionary[value[..separatorIndex]] = value[(separatorIndex + 1)..];
        }

        return dictionary;
    }

    private static string ReadRequestedContext(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if ((string.Equals(args[index], "--context", StringComparison.Ordinal) || string.Equals(args[index], "-c", StringComparison.Ordinal)) && index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static bool IsStaticCommandRequest(string[] args)
    {
        ReadOnlySpan<string> commands =
        [
            "login",
            "logout",
            "context",
            "api",
            "docs",
            "completion",
            "doctor",
            "version",
        ];

        var startIndex = args.Length > 0 && string.Equals(args[0], "oc", StringComparison.Ordinal)
            ? 1
            : 0;

        for (var index = startIndex; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--context" or "-c" or "--output")
            {
                index++;
                continue;
            }

            if (argument.StartsWith("--context=", StringComparison.Ordinal) ||
                argument.StartsWith("--output=", StringComparison.Ordinal) ||
                argument.StartsWith('-'))
            {
                continue;
            }

            return commands.Contains(argument, StringComparer.Ordinal);
        }

        return false;
    }

    private TenantContextRecord RequireContext(string? name)
    {
        var context = ContextStore.FindContext(_configuration, name);
        return context ?? throw new CliException(name is null
            ? "No context is selected. Add one with 'oc context add <name> <url>'."
            : $"Context '{name}' was not found.");
    }

    private static Uri GetAuthority(TenantContextRecord context) => new(context.Authority ?? context.TenantUrl, UriKind.Absolute);

    private static Uri GetManagementManifestUri(TenantContextRecord context) =>
        !string.IsNullOrWhiteSpace(context.ManagementManifestUrl)
            ? new Uri(context.ManagementManifestUrl, UriKind.Absolute)
            : new Uri(new Uri(context.TenantUrl, UriKind.Absolute), "api/management/manifest");

    private static Uri GetDocumentationBaseUri() =>
        new(DocumentationIndexUri.AbsoluteUri.Replace("search/search_index.json", string.Empty, StringComparison.Ordinal), UriKind.Absolute);

    private async Task<RemoteManagementManifest> FetchBootstrapAsync(string tenantUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(new Uri(new Uri(tenantUrl, UriKind.Absolute), RemoteManagementConstants.BootstrapPath), cancellationToken);
        response.EnsureSuccessStatusCode();
        return CliUtilities.ParseManifest(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<int> WriteOutputAsync(ParseResult parseResult, JsonElement element, CancellationToken cancellationToken, IReadOnlyList<CliTableColumnMetadata>? tableColumns = null)
    {
        var format = CliUtilities.ParseOutputFormat(parseResult.GetValue(_outputOption));
        await OutputFormatter.WriteAsync(new CommandOutput { Json = element, TableColumns = tableColumns }, format, Console.Out, cancellationToken);
        return 0;
    }

    private static async Task<string> ReadClientSecretAsync(string? environmentVariable, bool readFromStdin, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(environmentVariable))
        {
            var value = global::System.Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new CliException($"Environment variable '{environmentVariable}' was not set.");
        }

        if (readFromStdin)
        {
            var value = await Console.In.ReadToEndAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        throw new CliException("A client secret is required. Provide --client-secret-env or --client-secret-stdin.");
    }
}
