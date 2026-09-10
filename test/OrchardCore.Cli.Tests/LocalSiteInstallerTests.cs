using System.Text.Json;

namespace OrchardCore.Cli.Tests;

public class LocalSiteInstallerTests
{
    [Fact]
    public void SelectSdk_RequiresStableMatchingMajorAndChoosesNewestPatch()
    {
        var major = DotnetEnvironment.RequiredMajor;
        Assert.Equal($"{major}.0.302", DotnetEnvironment.SelectSdk($"""
            {major - 1}.0.999 [/sdk]
            {major}.0.100 [/sdk]
            {major}.0.302 [/sdk]
            {major}.0.400-preview.1 [/sdk]
            {major + 1}.0.100 [/sdk]
            """));
        Assert.Null(DotnetEnvironment.SelectSdk($"{major - 1}.0.999 [/sdk]\n{major}.0.100-preview.1 [/sdk]"));
        Assert.Null(DotnetEnvironment.SelectSdk(string.Empty));
    }

    [Fact]
    public async Task EmbeddedTemplate_ContainsOnlyCmsAndUsesThisBuildVersion()
    {
        var directory = TestPaths.CreateScratchDirectory(nameof(EmbeddedTemplate_ContainsOnlyCmsAndUsesThisBuildVersion));
        await LocalSiteInstaller.ExtractTemplateAsync(directory, TestContext.Current.CancellationToken);
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, ".template.config", "template.json"), TestContext.Current.CancellationToken));
        Assert.Equal("occms", config.RootElement.GetProperty("shortName").GetString());
        Assert.Equal(DotnetEnvironment.PackageVersion, config.RootElement.GetProperty("symbols").GetProperty("OrchardVersion").GetProperty("defaultValue").GetString());
        Assert.Equal($"net{DotnetEnvironment.RequiredMajor}.0", config.RootElement.GetProperty("symbols").GetProperty("Framework").GetProperty("defaultValue").GetString());
        Assert.Single(Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(directory, "Program.cs")));
        Assert.True(File.Exists(Path.Combine(directory, "NLog.config")));
        Assert.False(Directory.Exists(Path.Combine(directory, ".template.config.src")));
    }

    [Fact]
    public void Validate_RefusesExistingContentWithoutChangingIt()
    {
        var options = CreateOptions();
        var existing = Path.Combine(options.Directory, "keep.txt");
        File.WriteAllText(existing, "keep me");
        Assert.Throws<CliException>(() => LocalSiteInstaller.Validate(options));
        Assert.Equal("keep me", File.ReadAllText(existing));
        Assert.Single(Directory.GetFiles(options.Directory));
    }

    [Fact]
    public void Validate_RefusesDestinationSymlink()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = TestPaths.CreateScratchDirectory(nameof(Validate_RefusesDestinationSymlink));
        var target = Path.Combine(directory, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(directory, "link");
        Directory.CreateSymbolicLink(link, target);
        Assert.Throws<CliException>(() => LocalSiteInstaller.Validate(CreateOptions(link)));
        Assert.Empty(Directory.GetFileSystemEntries(target));
    }

    [Fact]
    public void SetupProcess_UsesEnvironmentForSecretsAndArgumentListForPaths()
    {
        var options = CreateOptions();
        options.Password = "not-on-command-line";
        options.ConnectionString = "Server=localhost;Password=not-on-command-line";
        var environment = LocalSiteInstaller.CreateSetupEnvironment(options, "/random-setup");
        var info = DotnetEnvironment.CreateStartInfo(["path with spaces/site.dll", "--urls", "http://127.0.0.1:12345"], options.Directory, environment);
        Assert.False(info.UseShellExecute);
        Assert.Equal("path with spaces/site.dll", info.ArgumentList[0]);
        Assert.DoesNotContain(options.Password, string.Join(' ', info.ArgumentList));
        Assert.Equal(options.Password, info.Environment["OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminPassword"]);
        Assert.Equal(options.ConnectionString, info.Environment["OrchardCore__OrchardCore_AutoSetup__Tenants__0__DatabaseConnectionString"]);
        Assert.Equal("Default", info.Environment["OrchardCore__OrchardCore_AutoSetup__Tenants__0__ShellName"]);
        var normalRun = DotnetEnvironment.CreateStartInfo(["site.dll"], options.Directory, null);
        Assert.DoesNotContain(normalRun.Environment.Keys, key => key.StartsWith("OrchardCore__OrchardCore_AutoSetup__", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.GetFileSystemEntries(options.Directory));
    }

    [Theory]
    [InlineData("Uninitialized", false)]
    [InlineData("Initializing", false)]
    [InlineData("Running", true)]
    [InlineData("Disabled", false)]
    public async Task SetupCompletion_RequiresPersistedRunningDefaultTenant(string state, bool expected)
    {
        var directory = TestPaths.CreateScratchDirectory(nameof(SetupCompletion_RequiresPersistedRunningDefaultTenant));
        Assert.False(await LocalSiteInstaller.IsInitializedAsync(directory, TestContext.Current.CancellationToken));
        Directory.CreateDirectory(Path.Combine(directory, "App_Data"));
        await File.WriteAllTextAsync(Path.Combine(directory, "App_Data", "tenants.json"), $$$"""{"Default":{"State":"{{{state}}}"}}""", TestContext.Current.CancellationToken);
        Assert.Equal(expected, await LocalSiteInstaller.IsInitializedAsync(directory, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessLog_RedactsSecrets()
    {
        await using var input = new MemoryStream("password=a-secret\nconnection=another-secret"u8.ToArray());
        using var reader = new StreamReader(input);
        using var output = new StringWriter();
        await DotnetEnvironment.DrainAsync(reader, output, secrets: ["a-secret", "another-secret"]);
        Assert.DoesNotContain("a-secret", output.ToString());
        Assert.DoesNotContain("another-secret", output.ToString());
        Assert.Contains("[redacted]", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallLog_ShowsDiagnosticsOnFailureOrWhenVerbose(bool verbose)
    {
        using var output = new StringWriter();
        using var log = new InstallProcessLog(output, verbose);
        using var reader = new StreamReader(new MemoryStream("setup failed: a-secret"u8.ToArray()));
        await DotnetEnvironment.DrainAsync(reader, log, secrets: ["a-secret"]);
        Assert.Equal(verbose, output.ToString().Contains("setup failed", StringComparison.Ordinal));
        await log.WriteFailureAsync();
        Assert.Contains("setup failed: [redacted]", output.ToString());
        Assert.DoesNotContain("a-secret", output.ToString());
        Assert.Equal(1, output.ToString().Split("setup failed").Length - 1);
    }

    [Fact]
    public async Task InstallLog_BoundsDiagnosticsAndRetainsLatestFailure()
    {
        using var output = new StringWriter();
        using var log = new InstallProcessLog(output, verbose: false);
        await log.WriteLineAsync("old output");
        await log.WriteLineAsync(new string('x', InstallProcessLog.Capacity * 2));
        await log.WriteLineAsync("latest failure");
        Assert.Empty(output.ToString());
        await log.WriteFailureAsync();
        Assert.DoesNotContain("old output", output.ToString());
        Assert.Contains("earlier output omitted", output.ToString());
        Assert.Contains("latest failure", output.ToString());
        Assert.True(output.ToString().Length < InstallProcessLog.Capacity + 200);
    }

    [Fact]
    public async Task InstallHelp_IsLocalAndDoesNotOfferInlineSecrets()
    {
        var paths = new CliPaths(TestPaths.CreateScratchDirectory(nameof(InstallHelp_IsLocalAndDoesNotOfferInlineSecrets)));
        using var client = new HttpClient(new RejectNetworkHandler());
        await new ContextStore(paths).SaveAsync(new CliConfiguration
        {
            CurrentContext = "offline",
            Contexts = [new TenantContextRecord { Name = "offline", TenantUrl = "https://offline.example/" }],
        }, TestContext.Current.CancellationToken);
        var args = new[] { "install", "--help" };
        var app = await CliApplication.CreateAsync(args, paths, client, TestContext.Current.CancellationToken, new UnsupportedCredentialStore());
        var install = Assert.Single(app.RootCommand.Subcommands, command => command.Name == "install");
        Assert.Contains(install.Options, option => option.Name == "--password-env");
        Assert.Contains(install.Options, option => option.Name == "--password-file");
        Assert.Contains(install.Options, option => option.Name == "--password-stdin");
        Assert.DoesNotContain(install.Options, option => option.Name is "--password" or "--connection-string" or "--body" or "--version");
        Assert.Equal(0, await app.InvokeAsync(args));
    }

    [Fact]
    public async Task Install_RefusesReadingTwoSecretsFromStdinBeforeCreatingProject()
    {
        var directory = TestPaths.CreateScratchDirectory(nameof(Install_RefusesReadingTwoSecretsFromStdinBeforeCreatingProject));
        using var client = new HttpClient(new RejectNetworkHandler());
        var args = new[] { "install", directory, "--site-name", "Test", "--email", "admin@example.com", "--password-stdin", "--connection-string-stdin" };
        var app = await CliApplication.CreateAsync(args, new CliPaths(Path.Combine(directory, "config")), client, TestContext.Current.CancellationToken, new UnsupportedCredentialStore());
        using var errors = new StringWriter();
        Assert.Equal(1, await app.InvokeAsync(args, errors));
        Assert.Contains("Only one secret can be read from stdin", errors.ToString());
        Assert.Empty(Directory.GetFiles(directory, "*.csproj"));
    }

    private static LocalSiteInstallOptions CreateOptions(string? directory = null) => new()
    {
        Directory = directory ?? TestPaths.CreateScratchDirectory("install-options"),
        SiteName = "Local site",
        UserName = "admin",
        Email = "admin@example.com",
    };

    private sealed class RejectNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Local install discovery must not contact a tenant.");
    }
}
