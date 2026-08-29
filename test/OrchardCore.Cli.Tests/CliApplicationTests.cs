using System.CommandLine;

namespace OrchardCore.Cli.Tests;

public class CliApplicationTests
{
    [Fact]
    public async Task InvokeAsync_ContextRequiredWithoutSelection_WritesFriendlyErrorAndReturnsFailure()
    {
        var paths = new CliPaths(TestPaths.CreateScratchDirectory(nameof(InvokeAsync_ContextRequiredWithoutSelection_WritesFriendlyErrorAndReturnsFailure)));
        using var httpClient = new HttpClient();
        using var errorWriter = new StringWriter();
        var args = new[] { "login" };
        var app = await CliApplication.CreateAsync(args, paths, httpClient, CancellationToken.None);

        var exitCode = await app.InvokeAsync(args, errorWriter);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            "Error: No context is selected. Add one with 'oc context add <name> <url>'.",
            errorWriter.ToString().Trim());
        Assert.DoesNotContain("Unhandled exception", errorWriter.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(CliException), errorWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_ContextClearForce_RemovesAllContexts()
    {
        var paths = new CliPaths(TestPaths.CreateScratchDirectory(nameof(InvokeAsync_ContextClearForce_RemovesAllContexts)));
        var store = new ContextStore(paths);
        await store.SaveAsync(new CliConfiguration
        {
            CurrentContext = "primary",
            Contexts =
            [
                new TenantContextRecord { Name = "primary", TenantUrl = "https://primary.example.com/" },
                new TenantContextRecord { Name = "secondary", TenantUrl = "https://secondary.example.com/" },
            ],
        }, CancellationToken.None);
        using var httpClient = new HttpClient();
        var args = new[] { "context", "clear", "--force" };
        var app = await CliApplication.CreateAsync(args, paths, httpClient, CancellationToken.None);

        var exitCode = await app.InvokeAsync(args);
        var configuration = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Null(configuration.CurrentContext);
        Assert.Empty(configuration.Contexts);
    }

    [Fact]
    public async Task CreateAsync_ContextClearWithRootCommandToken_DoesNotRequestDynamicMetadata()
    {
        var paths = new CliPaths(TestPaths.CreateScratchDirectory(nameof(CreateAsync_ContextClearWithRootCommandToken_DoesNotRequestDynamicMetadata)));
        var store = new ContextStore(paths);
        await store.SaveAsync(new CliConfiguration
        {
            CurrentContext = "primary",
            Contexts =
            [
                new TenantContextRecord { Name = "primary", TenantUrl = "https://primary.example.com/" },
            ],
        }, CancellationToken.None);
        var handler = new RequestCountingHandler();
        using var httpClient = new HttpClient(handler);
        var args = new[] { "oc", "context", "clear", "--force" };

        _ = await CliApplication.CreateAsync(args, paths, httpClient, CancellationToken.None);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CreateAsync_SecretRequestProperty_ExposesOnlySecretSafeOptions()
    {
        const string tenantUrl = "https://primary.example.com/";
        const string openApi = """
        {
          "paths": {
            "/api/tenants/{tenantName}:setup": {
              "post": {
                "parameters": [
                  {
                    "name": "tenantName",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "string" }
                  }
                ],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["siteName", "password"],
                        "properties": {
                          "siteName": { "type": "string" },
                          "password": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "x-oc-cli": {
                  "commandGroup": ["tenants"],
                  "verb": "setup",
                  "arguments": [{ "parameterName": "tenantName", "position": 0 }],
                  "secretProperties": ["password"]
                }
              }
            }
          }
        }
        """;
        var paths = new CliPaths(TestPaths.CreateScratchDirectory(nameof(CreateAsync_SecretRequestProperty_ExposesOnlySecretSafeOptions)));
        await new ContextStore(paths).SaveAsync(new CliConfiguration
        {
            CurrentContext = "primary",
            Contexts = [new TenantContextRecord { Name = "primary", TenantUrl = tenantUrl }],
        }, CancellationToken.None);
        await new CacheService(paths).WriteAsync(tenantUrl, CacheKind.OpenApi, new CachedContentRecord
        {
            Url = tenantUrl + "openapi.json",
            Content = openApi,
            FetchedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        }, CancellationToken.None);

        using var httpClient = new HttpClient();
        var app = await CliApplication.CreateAsync(["tenants", "setup", "--help"], paths, httpClient, CancellationToken.None);
        var tenants = Assert.Single(app.RootCommand.Subcommands, command => command.Name == "tenants");
        var setup = Assert.Single(tenants.Subcommands, command => command.Name == "setup");
        var optionNames = setup.Options.Select(option => option.Name).ToArray();

        Assert.DoesNotContain("--password", optionNames);
        Assert.DoesNotContain("--body", optionNames);
        Assert.Contains("--password-env", optionNames);
        Assert.Contains("--password-file", optionNames);
        Assert.Contains("--password-stdin", optionNames);
        Assert.False(setup.Options.Single(option => option.Name == "--site-name").Required);
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("YES", true)]
    [InlineData("", false)]
    [InlineData("no", false)]
    public async Task ConfirmContextClearAsync_Response_ReturnsExpectedResult(string response, bool expected)
    {
        using var input = new StringReader(response);
        using var promptWriter = new StringWriter();

        var result = await CliApplication.ConfirmContextClearAsync(2, input, promptWriter, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal("Delete all 2 saved contexts and stored credentials? [y/N] ", promptWriter.ToString());
    }

    [Theory]
    [InlineData("secret\r\n", "secret")]
    [InlineData("secret\n", "secret")]
    [InlineData("secret", "secret")]
    [InlineData("secret\n\n", "secret\n")]
    public void RemoveTrailingLineEnding_Input_RemovesOneLineEnding(string value, string expected)
    {
        Assert.Equal(expected, CliApplication.RemoveTrailingLineEnding(value));
    }

    [Fact]
    public async Task ResolveSecretValueAsync_FileOption_ReadsValueWithoutTrailingLineEnding()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "Secret1!\n", TestContext.Current.CancellationToken);
        try
        {
            var environmentOption = new Option<string?>("--password-env");
            var fileOption = new Option<FileInfo?>("--password-file");
            var stdinOption = new Option<bool>("--password-stdin");
            var command = new RootCommand();
            command.Options.Add(environmentOption);
            command.Options.Add(fileOption);
            command.Options.Add(stdinOption);
            var parseResult = command.Parse(["--password-file", path]);

            var value = await CliApplication.ResolveSecretValueAsync(
                parseResult,
                new SecretBodyPropertyOptions
                {
                    Property = new RequestBodyPropertyDefinition { Name = "password", Required = true },
                    EnvironmentVariableOption = environmentOption,
                    FileOption = fileOption,
                    StdinOption = stdinOption,
                },
                TestContext.Current.CancellationToken);

            Assert.Equal("Secret1!", value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RequestCountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
        }
    }

}
