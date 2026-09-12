using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;
using OrchardCore.Json;
using OrchardCore.Layers.Models;
using OrchardCore.Layers.Recipes;
using OrchardCore.Layers.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Rules;
using OrchardCore.Rules.Models;
using OrchardCore.Rules.Services;
using ISession = YesSql.ISession;

namespace OrchardCore.Tests.Modules.OrchardCore.Layers;

public class LayerRecipeTests
{
    [Theory]
    [InlineData(" ", "true")]
    [InlineData("Valid", "function (")]
    public async Task Execute_InvalidDefinition_DoesNotMutateDocument(string name, string script)
    {
        var document = new LayersDocument();
        var documents = new Mock<IDocumentManager<LayersDocument>>();
        documents.Setup(manager => manager.GetOrCreateMutableAsync()).ReturnsAsync(document);
        documents.Setup(manager => manager.GetOrCreateImmutableAsync()).ReturnsAsync(document);
        using var services = CreateServices(documents.Object);
        var context = CreateContext(new JsonObject
        {
            ["Name"] = name,
            ["LayerRule"] = new JsonObject
            {
                ["Conditions"] = new JsonArray(new JsonObject { ["Name"] = "JavascriptCondition", ["Script"] = script }),
            },
        });

        await ActivatorUtilities.CreateInstance<LayerStep>(services).ExecuteAsync(context);

        Assert.NotEmpty(context.Errors);
        Assert.Empty(document.Layers);
        documents.Verify(manager => manager.UpdateAsync(It.IsAny<LayersDocument>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UnknownConditionAfterValidEntry_PreservesExistingDocument()
    {
        var original = new Layer { Name = "Existing", Description = "Original", LayerRule = new Rule { ConditionId = "root" } };
        var document = new LayersDocument();
        document.Layers.Add(original);
        var documents = new Mock<IDocumentManager<LayersDocument>>();
        documents.Setup(manager => manager.GetOrCreateMutableAsync()).ReturnsAsync(document);
        documents.Setup(manager => manager.GetOrCreateImmutableAsync()).ReturnsAsync(document);
        using var services = CreateServices(documents.Object);
        var context = CreateContext(new JsonObject { ["Name"] = "Existing", ["Description"] = "Changed" },
            new JsonObject
            {
                ["Name"] = "New",
                ["LayerRule"] = new JsonObject
                {
                    ["Conditions"] = new JsonArray(new JsonObject { ["Name"] = "UnknownCondition" }),
                },
            });

        await ActivatorUtilities.CreateInstance<LayerStep>(services).ExecuteAsync(context);

        Assert.NotEmpty(context.Errors);
        Assert.Same(original, Assert.Single(document.Layers));
        Assert.Equal("Original", original.Description);
        documents.Verify(manager => manager.UpdateAsync(It.IsAny<LayersDocument>()), Times.Never);
    }

    private static RecipeExecutionContext CreateContext(params JsonNode[] layers) => new()
    {
        Name = "Layers",
        Step = new JsonObject { ["Layers"] = new JsonArray(layers) },
    };

    private static ServiceProvider CreateServices(IDocumentManager<LayersDocument> documents)
    {
        var ids = new Mock<IConditionIdGenerator>();
        ids.Setup(generator => generator.GenerateUniqueId(It.IsAny<Condition>()))
            .Callback<Condition>(condition => condition.ConditionId = Guid.NewGuid().ToString("N"));
        return new ServiceCollection().AddLogging().AddLocalization()
            .AddSingleton(documents).AddSingleton(Mock.Of<ISession>()).AddSingleton(ids.Object)
            .AddSingleton<IConditionFactory, ConditionFactory<JavascriptCondition>>()
            .AddSingleton(Options.Create(new DocumentJsonSerializerOptions()))
            .AddSingleton(Options.Create(new ConditionOperatorOptions()))
            .AddSingleton<ILayerService, LayerService>()
            .AddSingleton<IRuleManagementService, RuleManagementService>()
            .BuildServiceProvider();
    }
}
