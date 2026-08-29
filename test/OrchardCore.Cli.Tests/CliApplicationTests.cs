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
