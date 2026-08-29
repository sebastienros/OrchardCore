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

}
