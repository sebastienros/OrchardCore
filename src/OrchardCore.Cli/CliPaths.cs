using System.Security.Cryptography;
using System.Text;

namespace OrchardCore.Cli;

internal sealed class CliPaths
{
    public CliPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = rootDirectory;
        ConfigFilePath = Path.Combine(rootDirectory, "contexts.json");
        CacheDirectory = Path.Combine(rootDirectory, "cache");
    }

    public string RootDirectory { get; }

    public string ConfigFilePath { get; }

    public string CacheDirectory { get; }

    public void EnsureRootDirectory()
    {
        Directory.CreateDirectory(RootDirectory);
        SetOwnerOnlyDirectory(RootDirectory);
    }

    public static CliPaths CreateDefault() => new(GetDefaultRootDirectory());

    public static string NormalizeTenantUrl(string tenantUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantUrl);

        var builder = new UriBuilder(tenantUrl)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };

        if (!builder.Path.EndsWith('/'))
        {
            builder.Path = $"{builder.Path.TrimEnd('/')}/";
        }

        return builder.Uri.AbsoluteUri;
    }

    public string GetCacheFilePath(string tenantUrl, CacheKind kind)
    {
        var directory = Path.Combine(CacheDirectory, ComputeContextKey(tenantUrl));
        Directory.CreateDirectory(directory);
        SetOwnerOnlyDirectory(CacheDirectory);
        SetOwnerOnlyDirectory(directory);

        return Path.Combine(directory, kind switch
        {
            CacheKind.Manifest => "manifest.json",
            CacheKind.OpenApi => "openapi.json",
            CacheKind.Documentation => "documentation.json",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });
    }

    private static string ComputeContextKey(string tenantUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeTenantUrl(tenantUrl)));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    public static void SetOwnerOnlyFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void SetOwnerOnlyDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static string GetDefaultRootDirectory()
    {
        string baseDirectory;

        if (OperatingSystem.IsWindows())
        {
            baseDirectory = global::System.Environment.GetFolderPath(
                global::System.Environment.SpecialFolder.ApplicationData,
                global::System.Environment.SpecialFolderOption.Create);
            return Path.Combine(baseDirectory, "OrchardCore", "oc");
        }

        if (OperatingSystem.IsMacOS())
        {
            baseDirectory = Path.Combine(
                global::System.Environment.GetFolderPath(
                    global::System.Environment.SpecialFolder.UserProfile,
                    global::System.Environment.SpecialFolderOption.Create),
                "Library",
                "Application Support");
            return Path.Combine(baseDirectory, "OrchardCore", "oc");
        }

        baseDirectory = global::System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(
                global::System.Environment.GetFolderPath(
                    global::System.Environment.SpecialFolder.UserProfile,
                    global::System.Environment.SpecialFolderOption.Create),
                ".config");
        return Path.Combine(baseDirectory, "orchardcore", "oc");
    }
}
