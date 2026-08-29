using System.Text.Json.Nodes;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Cli.Tests;

public class CliApplicationSchemaTests
{
    [Fact]
    public void SelectSchemaOperation_IdenticalSchemasWithoutSelection_ReturnsFirstOperation()
    {
        var create = CreateOperation("create", "string");
        var update = CreateOperation("update", "string");

        var selected = CliApplication.SelectSchemaOperation([create, update], null);

        Assert.Same(create, selected);
    }

    [Fact]
    public void SelectSchemaOperation_DifferentSchemasWithoutSelection_RequiresOperation()
    {
        var operations = new[]
        {
            CreateOperation("create", "string"),
            CreateOperation("update", "integer"),
        };

        var exception = Assert.Throws<CliException>(() => CliApplication.SelectSchemaOperation(operations, null));

        Assert.Contains("--operation", exception.Message);
        Assert.Contains("create, update", exception.Message);
    }

    [Fact]
    public void SelectSchemaOperation_ExplicitAlias_ReturnsMatchingOperation()
    {
        var create = CreateOperation("create", "string");
        create.CliMetadata.Aliases.Add("add");

        var selected = CliApplication.SelectSchemaOperation([create], "ADD");

        Assert.Same(create, selected);
    }

    private static OpenApiOperationDefinition CreateOperation(string verb, string valueType) =>
        new()
        {
            CliMetadata = new CliOperationMetadata(["widgets"], verb),
            RequestBodySchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["value"] = new JsonObject
                    {
                        ["type"] = valueType,
                    },
                },
            },
        };
}
