namespace OrchardCore.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var httpClient = CliHttp.CreateClient();
        var app = await CliApplication.CreateAsync(
            args,
            CliPaths.CreateDefault(),
            httpClient,
            CancellationToken.None);

        return await app.InvokeAsync(args);
    }
}
