using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OrchardCore.Cli;

internal sealed class LocalSiteInstallOptions
{
    public required string Directory { get; init; }
    public required string SiteName { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string RecipeName { get; init; } = "SaaS";
    public string DatabaseProvider { get; init; } = "Sqlite";
    public string? TablePrefix { get; init; }
    public string? Schema { get; init; }
    public string SiteTimeZone { get; init; } = "UTC";
    public string? RequestUrlPrefix { get; init; }
    public string? RequestUrlHost { get; init; }
    public string? Source { get; init; }
    public string Urls { get; init; } = "http://localhost:5000";
    public int SetupTimeoutSeconds { get; init; } = 300;
    public bool Verbose { get; init; }
    public string Password { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public string[] SecretEnvironmentVariables { get; init; } = [];
}

internal sealed class LocalSiteInstallOutput
{
    public string Directory { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string PackageVersion { get; init; } = string.Empty;
    public string SdkVersion { get; init; } = string.Empty;
    public string Tenant { get; init; } = "Default";
    public string TenantState { get; init; } = "Running";
    public string Url { get; init; } = string.Empty;
    public string ListenUrl { get; init; } = string.Empty;
}

internal static partial class LocalSiteInstaller
{
    internal const string FeedzSource = "https://f.feedz.io/sebastienros/orchardcore/nuget/index.json";
    internal const string NugetSource = "https://api.nuget.org/v3/index.json";
    private const string AutoSetupPrefix = "OrchardCore__OrchardCore_AutoSetup__";

    public static void Validate(LocalSiteInstallOptions options)
    {
        var directory = new DirectoryInfo(options.Directory);
        if (File.Exists(options.Directory) || directory.LinkTarget is not null || directory.Exists && directory.EnumerateFileSystemInfos().Any())
        {
            throw new CliException("Choose a new or empty directory. 'oc install' does not overwrite an existing site or follow a destination symlink.");
        }

        if (string.IsNullOrWhiteSpace(options.SiteName) || string.IsNullOrWhiteSpace(options.UserName)
            || string.IsNullOrWhiteSpace(options.RecipeName) || !MailAddress.TryCreate(options.Email, out var email) || email.Address != options.Email)
        {
            throw new CliException("Provide a site name, user name, valid email address, and setup recipe name.");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseProvider) || string.IsNullOrWhiteSpace(options.SiteTimeZone))
        {
            throw new CliException("Provide a database provider and site time zone.");
        }

        if (options.SetupTimeoutSeconds is < 1 or > 3600)
        {
            throw new CliException("--setup-timeout must be between 1 and 3600 seconds.");
        }

        if (!string.IsNullOrEmpty(options.RequestUrlPrefix) && !UrlPrefixPattern().IsMatch(options.RequestUrlPrefix))
        {
            throw new CliException("--request-url-prefix must contain path segments made of letters, digits, underscores, or hyphens, without leading or trailing slashes.");
        }

        if (!string.IsNullOrEmpty(options.RequestUrlHost) && Uri.CheckHostName(options.RequestUrlHost) == UriHostNameType.Unknown)
        {
            throw new CliException("--request-url-host must be a single host name without a scheme, port, or path.");
        }

        if (!Uri.TryCreate(options.Urls, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.Port == 0 || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo + uri.Query + uri.Fragment))
        {
            throw new CliException("--urls must be one HTTP or HTTPS listen URL without credentials, path, query, or fragment.");
        }

        _ = ResolveSource(options);
    }

    public static void ValidateSecrets(LocalSiteInstallOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new CliException("The administrator password cannot be empty.");
        }

        if (!string.Equals(options.DatabaseProvider, "Sqlite", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new CliException("Provide --connection-string-env, --connection-string-file, or --connection-string-stdin for this database provider.");
        }
    }

    private static string ResolveSource(LocalSiteInstallOptions options)
    {
        var source = options.Source ?? (DotnetEnvironment.PackageVersion.Contains("-cli.", StringComparison.Ordinal) ? FeedzSource : NugetSource);
        if (Directory.Exists(source))
        {
            return Path.GetFullPath(source);
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo + uri.Query + uri.Fragment))
        {
            throw new CliException("--source must be an HTTPS NuGet feed URL without credentials, query, or fragment, or an existing local package directory.");
        }

        return source;
    }

    internal static async Task ExtractTemplateAsync(string destination, CancellationToken cancellationToken)
    {
        var assembly = typeof(Program).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("OccmsTemplate/", StringComparison.Ordinal)))
        {
            var relative = name["OccmsTemplate/".Length..].Replace('\\', '/').Replace(".template.config.src/", ".template.config/", StringComparison.Ordinal);
            var path = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var input = assembly.GetManifestResourceStream(name)!;
            await using var output = new FileStream(path, FileMode.CreateNew);
            await input.CopyToAsync(output, cancellationToken);
        }

        var configPath = Path.Combine(destination, ".template.config", "template.json");
        if (!File.Exists(configPath))
        {
            throw new CliException("The embedded CMS template is missing. Reinstall this CLI build.");
        }

        var config = await File.ReadAllTextAsync(configPath, cancellationToken);
        config = config.Replace("${TemplateOrchardVersion}", DotnetEnvironment.PackageVersion, StringComparison.Ordinal)
            .Replace("${TemplateTargetFramework}", $"net{DotnetEnvironment.RequiredMajor}.0", StringComparison.Ordinal);
        await File.WriteAllTextAsync(configPath, config, cancellationToken);
    }

    public static async Task<LocalSiteInstallOutput> InstallAsync(LocalSiteInstallOptions options, string sdk, TextWriter log, CancellationToken cancellationToken)
    {
        Validate(options);
        ValidateSecrets(options);
        using var diagnostics = new InstallProcessLog(log, options.Verbose);
        var completed = false;
        var scratch = Path.Combine(Path.GetTempPath(), "oc-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(scratch, "dotnet-home"),
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
        };
        try
        {
            await log.WriteLineAsync($"Creating CMS {DotnetEnvironment.PackageVersion} from the embedded template using .NET SDK {sdk}.");
            var template = Path.Combine(scratch, "template");
            await ExtractTemplateAsync(template, cancellationToken);
            await WriteSdkSelectionAsync(scratch, sdk, cancellationToken);
            await DotnetEnvironment.RunAsync(["new", "install", template], scratch, environment, diagnostics, cancellationToken, options.SecretEnvironmentVariables);
            // Recheck after template extraction, before writing any project files.
            Validate(options);
            await DotnetEnvironment.RunAsync(["new", "occms", "--output", options.Directory], scratch, environment, diagnostics, cancellationToken, options.SecretEnvironmentVariables);
            await WriteSdkSelectionAsync(options.Directory, sdk, cancellationToken);
            var config = new XDocument(new XElement("configuration", new XElement("packageSources",
                new XElement("clear"), new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", NugetSource)))));
            var source = ResolveSource(options);
            if (source != NugetSource)
            {
                config.Root!.Element("packageSources")!.Add(new XElement("add", new XAttribute("key", "OrchardCore"), new XAttribute("value", source)));
            }

            var nugetConfig = Path.Combine(options.Directory, "NuGet.Config");
            await File.WriteAllTextAsync(nugetConfig, config.ToString(), cancellationToken);
            var programPath = Path.Combine(options.Directory, "Program.cs");
            var program = await File.ReadAllTextAsync(programPath, cancellationToken);
            const string orchardBuilder = ".AddOrchardCms()";
            if (program.Split(orchardBuilder).Length != 2)
            {
                throw new CliException("The embedded CMS template has an unsupported startup structure.");
            }

            await File.WriteAllTextAsync(programPath, program.Replace(orchardBuilder,
                orchardBuilder + global::System.Environment.NewLine + "    .AddSetupFeatures(\"OrchardCore.AutoSetup\")", StringComparison.Ordinal), cancellationToken);
            var project = Directory.GetFiles(options.Directory, "*.csproj").Single();
            await log.WriteLineAsync("Restoring and building the new site.");
            await DotnetEnvironment.RunAsync(["restore", project, "--configfile", nugetConfig, "--disable-build-servers"], options.Directory, environment, diagnostics, cancellationToken, options.SecretEnvironmentVariables);
            await DotnetEnvironment.RunAsync(["build", project, "--no-restore", "--disable-build-servers", "-m:1"], options.Directory, environment, diagnostics, cancellationToken, options.SecretEnvironmentVariables);
            await log.WriteLineAsync("Initializing the Default tenant.");
            await SetupAsync(options, project, diagnostics, cancellationToken);
            await log.WriteLineAsync($"Site setup completed in '{options.Directory}'. To start it later, run 'dotnet run --no-launch-profile' from that directory.");
            var uri = new UriBuilder(options.Urls) { Path = options.RequestUrlPrefix ?? string.Empty };
            if (!string.IsNullOrEmpty(options.RequestUrlHost))
            {
                uri.Host = options.RequestUrlHost;
            }

            completed = true;
            return new LocalSiteInstallOutput
            {
                Directory = options.Directory,
                Project = project,
                PackageVersion = DotnetEnvironment.PackageVersion,
                SdkVersion = sdk,
                Url = uri.Uri.AbsoluteUri,
                ListenUrl = options.Urls,
            };
        }
        finally
        {
            if (!completed)
            {
                await diagnostics.WriteFailureAsync();
            }

            options.Password = string.Empty;
            options.ConnectionString = null;
            try
            {
                Directory.Delete(scratch, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await log.WriteLineAsync($"Warning: could not remove temporary template files in '{scratch}'.");
            }
        }
    }

    private static Task WriteSdkSelectionAsync(string directory, string sdk, CancellationToken cancellationToken) => File.WriteAllTextAsync(
        Path.Combine(directory, "global.json"), new JsonObject
        {
            ["sdk"] = new JsonObject { ["version"] = sdk, ["rollForward"] = "latestPatch", ["allowPrerelease"] = false },
        }.ToJsonString(), cancellationToken);

    internal static Dictionary<string, string> CreateSetupEnvironment(LocalSiteInstallOptions options, string setupPath)
    {
        var values = new Dictionary<string, string>
        {
            ["ShellName"] = "Default", ["SiteName"] = options.SiteName, ["SiteTimeZone"] = options.SiteTimeZone,
            ["AdminUsername"] = options.UserName, ["AdminEmail"] = options.Email, ["AdminPassword"] = options.Password,
            ["RecipeName"] = options.RecipeName, ["DatabaseProvider"] = options.DatabaseProvider,
            ["DatabaseConnectionString"] = options.ConnectionString ?? string.Empty,
            ["DatabaseTablePrefix"] = options.TablePrefix ?? string.Empty, ["DatabaseSchema"] = options.Schema ?? string.Empty,
            ["RequestUrlPrefix"] = options.RequestUrlPrefix ?? string.Empty, ["RequestUrlHost"] = options.RequestUrlHost ?? string.Empty,
        };
        var environment = values.ToDictionary(pair => AutoSetupPrefix + "Tenants__0__" + pair.Key, pair => pair.Value);
        environment[AutoSetupPrefix + "AutoSetupPath"] = setupPath;
        return environment;
    }

    private static async Task SetupAsync(LocalSiteInstallOptions options, string project, TextWriter log, CancellationToken cancellationToken)
    {
        var appData = Path.Combine(options.Directory, "App_Data");
        Directory.CreateDirectory(appData);
        CliPaths.SetOwnerOnlyDirectory(appData);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.SetupTimeoutSeconds));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var url = $"http://127.0.0.1:{port}";
        var setupPath = "/oc-setup-" + Guid.NewGuid().ToString("N");
        await log.WriteLineAsync("Auto Setup uses a temporary local-only server; it stops before the requested site URL starts.");
        using var process = DotnetEnvironment.Start([ApplicationPath(project), "--urls", url], options.Directory,
            CreateSetupEnvironment(options, setupPath), options.SecretEnvironmentVariables);
        string[] secrets = [options.Password, options.ConnectionString ?? string.Empty];
        var stdout = DotnetEnvironment.DrainAsync(process.StandardOutput, log, secrets: secrets);
        var stderr = DotnetEnvironment.DrainAsync(process.StandardError, log, secrets: secrets);
        try
        {
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    throw new CliException("The setup host exited before setup completed. See the application output above.");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url + setupPath);
                if (!string.IsNullOrEmpty(options.RequestUrlHost))
                {
                    request.Headers.Host = options.RequestUrlHost;
                }

                try
                {
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if (response.StatusCode != HttpStatusCode.Redirect || !await IsInitializedAsync(options.Directory, timeout.Token))
                    {
                        throw new CliException($"Auto Setup did not complete (HTTP {(int)response.StatusCode}). Check the recipe, database settings, and password requirements in the application output above. The project is preserved.");
                    }

                    return;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(250, timeout.Token);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CliException($"Auto Setup exceeded {options.SetupTimeoutSeconds} seconds. The temporary host was stopped and the project is preserved.");
        }
        finally
        {
            await DotnetEnvironment.StopAsync(process);
            await Task.WhenAll(stdout, stderr);
        }
    }

    internal static async Task<bool> IsInitializedAsync(string directory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "App_Data", "tenants.json");
        if (!File.Exists(path))
        {
            return false;
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        return document.RootElement.TryGetProperty("Default", out var tenant)
            && tenant.TryGetProperty("State", out var state) && state.GetString() == "Running";
    }

    private static string ApplicationPath(string project) => Path.Combine(Path.GetDirectoryName(project)!, "bin", "Debug",
        $"net{DotnetEnvironment.RequiredMajor}.0", Path.GetFileNameWithoutExtension(project) + ".dll");

    public static async Task RunAsync(LocalSiteInstallOutput output, TextWriter log, CancellationToken cancellationToken,
        IEnumerable<string>? secretEnvironmentVariables = null)
    {
        await log.WriteLineAsync($"Starting {output.Url} — press Ctrl+C to stop.");
        await DotnetEnvironment.RunAsync([ApplicationPath(output.Project), "--urls", output.ListenUrl], output.Directory, null, log, cancellationToken, secretEnvironmentVariables);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+(/[a-zA-Z0-9_-]+)*$")]
    private static partial Regex UrlPrefixPattern();
}
