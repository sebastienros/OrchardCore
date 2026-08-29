using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents.Models;
using OrchardCore.Contents.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Contents;

public class ContentItemSchemaBuilderTests
{
    [Fact]
    public void ContentItemsResponse_UsesCamelCaseEnvelopeWithDocumentContent()
    {
        var response = new ContentItemsResponse
        {
            Skip = 1,
            Take = 2,
            TotalCount = 3,
            Items = [new ContentItem { ContentType = "Article" }],
        };

        var json = JsonSerializer.SerializeToNode(response, JsonSerializerOptions.Default)!.AsObject();

        Assert.Equal(1, json["skip"]!.GetValue<int>());
        Assert.Equal(2, json["take"]!.GetValue<int>());
        Assert.Equal(3, json["totalCount"]!.GetValue<int>());
        Assert.NotNull(json["items"]);
        Assert.Null(json["Skip"]);
    }

    [Fact]
    public void BuildSchema_IncludesContentItemEnvelopeAndNestedContentSchemas()
    {
        var schema = BuildSchema();
        var properties = Assert.IsType<JsonObject>(schema["properties"]);

        Assert.Equal("Article", properties["ContentType"]?["const"]?.GetValue<string>());
        Assert.Contains(nameof(ContentItem.ContentItemId), properties);
        Assert.Contains(nameof(ContentItem.ContentItemVersionId), properties);
        Assert.Contains(nameof(ContentItem.DisplayText), properties);
        Assert.Contains(nameof(ContentItem.Latest), properties);
        Assert.Contains(nameof(ContentItem.Published), properties);
        Assert.Contains(nameof(ContentItem.ModifiedUtc), properties);
        Assert.Contains(nameof(ContentItem.PublishedUtc), properties);
        Assert.Contains(nameof(ContentItem.CreatedUtc), properties);
        Assert.Contains(nameof(ContentItem.Owner), properties);
        Assert.Contains(nameof(ContentItem.Author), properties);

        var part = Assert.IsType<JsonObject>(properties["Article/Part~1"]);
        var partProperties = Assert.IsType<JsonObject>(part["properties"]);
        var details = Assert.IsType<JsonObject>(partProperties[nameof(SchemaPart.Details)]);
        Assert.Equal("Part details.", details["description"]?.GetValue<string>());
        Assert.NotNull(details["properties"]?[nameof(SchemaDetails.Name)]);

        var field = Assert.IsType<JsonObject>(partProperties["Related/Field~1"]);
        var fieldDetails = field["properties"]?[nameof(SchemaField.Details)];
        Assert.Equal("Field details.", fieldDetails?["description"]?.GetValue<string>());
        Assert.NotNull(fieldDetails?["properties"]?[nameof(SchemaDetails.Name)]);
    }

    [Fact]
    public void BuildSchema_RebasesNestedLocalReferences()
    {
        var schema = BuildSchema();
        var part = schema["properties"]?["Article/Part~1"];
        var field = part?["properties"]?["Related/Field~1"];
        var partReferences = GetReferences(part)
            .Where(reference => !reference.Contains("Related~1Field", StringComparison.Ordinal))
            .ToArray();
        var fieldReferences = GetReferences(field).ToArray();

        Assert.NotEmpty(partReferences);
        Assert.All(partReferences, reference =>
            Assert.StartsWith("#/properties/Article~1Part~01/", reference, StringComparison.Ordinal));
        Assert.NotEmpty(fieldReferences);
        Assert.All(fieldReferences, reference =>
            Assert.StartsWith(
                "#/properties/Article~1Part~01/properties/Related~1Field~01/",
                reference,
                StringComparison.Ordinal));
    }

    private static JsonObject BuildSchema()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddContentPart<SchemaPart>();
        services.AddContentField<SchemaField>();

        using var serviceProvider = services.BuildServiceProvider();
        var contentOptions = serviceProvider.GetRequiredService<IOptions<ContentOptions>>().Value;
        var fieldDefinition = new ContentPartFieldDefinition(
            new ContentFieldDefinition(nameof(SchemaField)),
            "Related/Field~1",
            []);
        var partDefinition = new ContentPartDefinition(nameof(SchemaPart), [fieldDefinition], []);
        var typePartDefinition = new ContentTypePartDefinition("Article/Part~1", partDefinition, []);
        var typeDefinition = new ContentTypeDefinition("Article", "Article", [typePartDefinition], []);

        return ContentItemSchemaBuilder.BuildSchema(
            typeDefinition,
            contentOptions,
            new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
    }

    private static IEnumerable<string> GetReferences(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject["$ref"]?.GetValue<string>() is { } reference)
            {
                yield return reference;
            }

            foreach (var property in jsonObject)
            {
                if (property.Value is not null)
                {
                    foreach (var nestedReference in GetReferences(property.Value))
                    {
                        yield return nestedReference;
                    }
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item is not null)
                {
                    foreach (var nestedReference in GetReferences(item))
                    {
                        yield return nestedReference;
                    }
                }
            }
        }
    }

    private sealed class SchemaPart : ContentPart
    {
        [Description("Part details.")]
        public SchemaDetails Details { get; set; }
    }

    private sealed class SchemaField : ContentField
    {
        [Description("Field details.")]
        public SchemaDetails Details { get; set; }
    }

    private sealed class SchemaDetails
    {
        public string Name { get; set; }

        public SchemaDetails Child { get; set; }
    }
}
