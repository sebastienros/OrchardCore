namespace OrchardCore.Cli.Tests;

public class CliUtilitiesTests
{
    [Theory]
    [InlineData("json", "Json")]
    [InlineData("table", "Table")]
    [InlineData("csv", "Csv")]
    [InlineData("tsv", "Tsv")]
    [InlineData("yaml", "Yaml")]
    [InlineData("toml", "Toml")]
    [InlineData("none", "None")]
    public void ParseOutputFormat_SupportedFormat_ReturnsFormat(string value, string expected)
    {
        Assert.Equal(expected, CliUtilities.ParseOutputFormat(value).ToString());
    }

    [Theory]
    [InlineData("jsonc")]
    public void ParseOutputFormat_RemovedFormat_ThrowsFriendlyCliException(string value)
    {
        var exception = Assert.Throws<CliException>(() => CliUtilities.ParseOutputFormat(value));

        Assert.Equal($"Unsupported output format '{value}'.", exception.Message);
    }

    [Theory]
    [InlineData("PageSize", "page-size")]
    [InlineData("featureId", "feature-id")]
    [InlineData("URLValue", "url-value")]
    [InlineData("already-kebab", "already-kebab")]
    [InlineData("snake_case", "snake-case")]
    public void ToCliName_OpenApiName_ReturnsLowerKebabCase(string value, string expected)
    {
        Assert.Equal(expected, CliUtilities.ToCliName(value));
    }

    [Fact]
    public void ParseManifest_NullOptionalUris_LeavesUrisUnset()
    {
        var manifest = CliUtilities.ParseManifest("""
            {
              "protocolMajorVersion": 1,
              "protocolMinorVersion": 0,
              "authentication": {
                "authority": "https://example.com/",
                "clientId": "orchardcore-cli",
                "grantTypes": ["authorization_code"],
                "scopes": ["orchardcore.management"]
              },
              "managementManifestUrl": "https://example.com/api/management/manifest",
              "openApiUrl": null,
              "jsonSchemaDialect": null,
              "documentationIndexUrl": null
            }
            """);

        Assert.Equal("https://example.com/api/management/manifest", manifest.ManagementManifestUrl.AbsoluteUri);
        Assert.Null(manifest.OpenApiUrl);
        Assert.Null(manifest.JsonSchemaDialect);
        Assert.Null(manifest.DocumentationIndexUrl);
    }

    [Fact]
    public void ParseManifest_InvalidUri_ThrowsFriendlyCliException()
    {
        var exception = Assert.Throws<CliException>(() => CliUtilities.ParseManifest("""
            {
              "managementManifestUrl": "not a URI"
            }
            """));

        Assert.Equal("JSON property 'managementManifestUrl' must be an absolute URI.", exception.Message);
    }
}
