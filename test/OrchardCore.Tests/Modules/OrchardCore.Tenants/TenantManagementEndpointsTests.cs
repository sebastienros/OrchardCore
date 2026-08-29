using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.RemoteManagement;
using OrchardCore.Tenants.Endpoints.Management;
using OrchardCore.Tenants.Models;
using OrchardCore.Tenants.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Tenants;

public class TenantManagementEndpointsTests
{
    [Fact]
    public async Task TenantCapabilityProvider_AdvertisesTenantManagement()
    {
        var capabilities = await new TenantRemoteManagementCapabilityProvider().GetCapabilitiesAsync();
        var capability = Assert.Single(capabilities);

        Assert.Equal(TenantManagementApiEndpointConventions.CapabilityName, capability.Id);
        Assert.Equal("Tenants", capability.DisplayName);
        Assert.Equal(
            $"{RemoteManagementConstants.ProtocolMajorVersion}.{RemoteManagementConstants.ProtocolMinorVersion}",
            capability.Version);
    }

    [Theory]
    [InlineData("", true, true, "")]
    [InlineData("styles/site.css", false, true, "styles/site.css")]
    [InlineData("/styles/site.css", false, false, "")]
    [InlineData("../site.css", false, false, "")]
    [InlineData("styles/./site.css", false, false, "")]
    public void TryNormalizePath_Input_EnforcesRelativeSafePaths(string path, bool allowEmpty, bool expected, string expectedPath)
    {
        var result = StaticFileManagementEndpoints.TryNormalizePath(path, allowEmpty, out var normalizedPath);

        Assert.Equal(expected, result);
        Assert.Equal(expectedPath, normalizedPath);
    }

    [Fact]
    public void TryResolvePhysicalPath_RootWithTrailingSeparator_ResolvesChild()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var result = StaticFileManagementEndpoints.TryResolvePhysicalPath(
                root + Path.DirectorySeparatorChar,
                "styles/site.css",
                out var physicalPath);

            Assert.True(result);
            Assert.Equal(Path.Combine(root, "styles", "site.css"), physicalPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateTenantResponse_RedactsSecretsAndBuildsUrls()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("admin.example.com");
        httpContext.Features.Set(new ShellContextFeature
        {
            OriginalPathBase = "/root",
        });

        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
            RequestUrlHost = "tenant-a.example.com,{host}",
            RequestUrlPrefix = "tenant-a",
            State = TenantState.Uninitialized,
        };

        shellSettings["Category"] = "Partners";
        shellSettings["Description"] = "Tenant description";
        shellSettings["DatabaseProvider"] = "SqlConnection";
        shellSettings["ConnectionString"] = "Server=db;User Id=orchard;Password=super-secret;";
        shellSettings["TablePrefix"] = "TenantA";
        shellSettings["Schema"] = "dbo";
        shellSettings["RecipeName"] = "Blog";
        shellSettings["FeatureProfile"] = "ProfileA, ProfileB";
        shellSettings["Secret"] = "tenant-secret";

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = TenantManagementEndpoints.CreateTenantResponse(
            httpContext,
            shellSettings,
            new EphemeralDataProtectionProvider(),
            clock.Object);

        Assert.Equal("TenantA", response.Name);
        Assert.Equal(["ProfileA", "ProfileB"], response.FeatureProfiles);
        Assert.Equal(["https://tenant-a.example.com/root/tenant-a"], response.Urls);
        Assert.Equal("https://tenant-a.example.com/root/tenant-a", response.PrimaryUrl);
        Assert.StartsWith("https://tenant-a.example.com/root/tenant-a?token=", response.SetupUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", response.ConnectionString, StringComparison.Ordinal);
        Assert.True(response.CanDelete);
        Assert.False(response.CanStart);
        Assert.False(response.CanStop);
    }

    [Theory]
    [InlineData("retry-safe", true)]
    [InlineData("different", false)]
    public async Task ContentEqualsAsync_Input_ReturnsExpectedResult(string content, bool expected)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "retry-safe", TestContext.Current.CancellationToken);
            await using var requestBody = new ShortReadStream(System.Text.Encoding.UTF8.GetBytes(content));

            var result = await StaticFileManagementEndpoints.ContentEqualsAsync(
                requestBody,
                path,
                TestContext.Current.CancellationToken);

            Assert.Equal(expected, result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MatchesCreateRequest_IdenticalSettings_ReturnsTrue()
    {
        var settings = new ShellSettings
        {
            Name = "TenantA",
            RequestUrlPrefix = "tenant-a",
        };
        settings["DatabaseProvider"] = "Sqlite";
        settings["TablePrefix"] = "TenantA";
        settings["RecipeName"] = "SaaS";
        var model = new TenantApiModel
        {
            Name = "TenantA",
            RequestUrlPrefix = "tenant-a",
            DatabaseProvider = "Sqlite",
            TablePrefix = "TenantA",
            RecipeName = "SaaS",
        };

        Assert.True(TenantManagementEndpoints.MatchesCreateRequest(settings, model));

        model.Description = "Different";
        Assert.False(TenantManagementEndpoints.MatchesCreateRequest(settings, model));
    }

    private sealed class ShortReadStream : MemoryStream
    {
        public ShortReadStream(byte[] buffer)
            : base(buffer)
        {
        }

        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            => base.ReadAsync(destination[..Math.Min(destination.Length, 2)], cancellationToken);
    }
}
