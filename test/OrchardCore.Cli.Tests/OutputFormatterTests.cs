using System.Text.Json;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Cli.Tests;

public class OutputFormatterTests
{
    [Fact]
    public async Task WriteAsync_WhenTableFormatIsRequested_UsesProvidedColumns()
    {
        using var document = JsonDocument.Parse("[{\"id\":\"a1\",\"name\":\"Alpha\"}]");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
            TableColumns = [new CliTableColumnMetadata("id", "Id"), new CliTableColumnMetadata("name", "Name")],
        }, OutputFormat.Table, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("Id", output, StringComparison.Ordinal);
        Assert.Contains("Alpha", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenYamlFormatIsRequested_FormatsNestedObject()
    {
        using var document = JsonDocument.Parse("{\"name\":\"tenant\",\"flags\":{\"enabled\":true}}");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
        }, OutputFormat.Yaml, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("name: \"tenant\"", output, StringComparison.Ordinal);
        Assert.Contains("enabled: true", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenCsvFormatIsRequested_EscapesValues()
    {
        using var document = JsonDocument.Parse("[{\"name\":\"Alpha, Inc.\",\"note\":\"He said \\\"hello\\\"\"}]");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
            TableColumns = [new CliTableColumnMetadata("name", "Name"), new CliTableColumnMetadata("note", "Note")],
        }, OutputFormat.Csv, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("\"Alpha, Inc.\"", output, StringComparison.Ordinal);
        Assert.Contains("\"He said \"\"hello\"\"\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenTsvFormatIsRequested_UsesTabSeparators()
    {
        using var document = JsonDocument.Parse("[{\"id\":\"a1\",\"name\":\"Alpha\"}]");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
            TableColumns = [new CliTableColumnMetadata("id", "Id"), new CliTableColumnMetadata("name", "Name")],
        }, OutputFormat.Tsv, writer, CancellationToken.None);

        Assert.Contains("Id\tName", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("a1\tAlpha", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenTomlFormatIsRequested_FormatsNestedObjectAndOmitsNulls()
    {
        using var document = JsonDocument.Parse(
            "{\"name\":\"tenant\",\"missing\":null,\"tags\":[\"one\",\"two\"],\"flags\":{\"enabled\":true}}");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
        }, OutputFormat.Toml, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("\"name\" = \"tenant\"", output, StringComparison.Ordinal);
        Assert.Contains("\"tags\" = [\"one\", \"two\"]", output, StringComparison.Ordinal);
        Assert.Contains("[\"flags\"]", output, StringComparison.Ordinal);
        Assert.Contains("\"enabled\" = true", output, StringComparison.Ordinal);
        Assert.DoesNotContain("missing", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenTomlRootArrayIsRequested_UsesItemsKey()
    {
        using var document = JsonDocument.Parse("[{\"id\":\"a1\"},{\"id\":\"a2\"}]");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
        }, OutputFormat.Toml, writer, CancellationToken.None);

        Assert.Contains("items = [{ \"id\" = \"a1\" }, { \"id\" = \"a2\" }]", writer.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("items.id")]
    [InlineData("items[].id")]
    public async Task WriteAsync_WhenTableUsesPagedItems_ExpandsItemRows(string propertyPath)
    {
        using var document = JsonDocument.Parse("{\"page\":1,\"items\":[{\"id\":\"a1\"},{\"id\":\"a2\"}]}");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
            TableColumns = [new CliTableColumnMetadata(propertyPath, "Id")],
        }, OutputFormat.Table, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("a1", output, StringComparison.Ordinal);
        Assert.Contains("a2", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenTableUsesNestedArray_ExpandsArrayRows()
    {
        using var document = JsonDocument.Parse(
            "{\"currentContext\":\"a\",\"contexts\":[{\"name\":\"a\"},{\"name\":\"b\"}]}");
        using var writer = new StringWriter();

        await OutputFormatter.WriteAsync(new CommandOutput
        {
            Json = document.RootElement.Clone(),
            TableColumns = [new CliTableColumnMetadata("contexts[].name", "Name")],
        }, OutputFormat.Table, writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("a", output, StringComparison.Ordinal);
        Assert.Contains("b", output, StringComparison.Ordinal);
    }
}
