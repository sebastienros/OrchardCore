using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement.Metadata.Settings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Models;
using OrchardCore.Environment.Shell;
using OrchardCore.Tests.Apis.Context;

namespace OrchardCore.Tests.Apis.ContentManagement.ContentDefinitionApi;

public class ContentDefinitionApiTests
{
    [Fact]
    public void ContentPartDefinitionSchema_DescribesKnownSettings()
    {
        var schema = JsonSerializerOptions.Web.GetJsonSchemaAsNode(typeof(ContentPartDefinitionDto));

        var contentPartSettings = schema["properties"]!["settings"]!["properties"]![nameof(ContentPartSettings)]!;
        Assert.Equal("boolean", contentPartSettings["properties"]!["attachable"]!["type"]!.GetValue<string>());
        Assert.Equal("boolean", contentPartSettings["properties"]!["reusable"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void ContentDefinitionSchemaBuilder_RepresentsDictionariesAsObjects()
    {
        var builderType = GetServiceType("OrchardCore.ContentTypes.Services.ContentDefinitionSchemaBuilder", "OrchardCore.ContentTypes");
        var method = builderType.GetMethod("BuildSchema", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var schema = (JsonObject)method.Invoke(null, [typeof(DictionarySchema), JsonSerializerOptions.Web])!;

        var values = schema["properties"]!["values"]!;
        Assert.Contains("object", values["type"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.NotNull(values["additionalProperties"]);
    }

    [Fact]
    public async Task ContentDefinitionService_CreatesAndReadsDefinitions()
    {
        using var context = new SiteContext();

        await context.InitializeAsync();
        await EnableFeaturesAsync(context, "OrchardCore.ContentTypes", "OrchardCore.ContentFields");
        await context.WaitForDeferredTasksAsync(TestContext.Current.CancellationToken);

        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService(GetServiceType("OrchardCore.ContentTypes.Services.ContentDefinitionApiService", "OrchardCore.ContentTypes"));
            var fieldTypes = Invoke<IReadOnlyList<ContentDefinitionFieldTypeDescriptor>>(service, "ListFieldTypes");
            var fieldTypeName = Assert.Single(fieldTypes, x => x.Name == "TextField").Name;

            var part = await InvokeAsync<ContentPartDefinitionDto>(service, "CreatePartAsync", new ContentPartDefinitionDto
            {
                Name = "RemoteTextPart",
                Settings = new ContentPartDefinitionSettingsDto
                {
                    ContentPartSettings = new ContentPartSettings
                    {
                        Attachable = true,
                        Reusable = true,
                    },
                },
            }, new ModelStateDictionary());

            var field = await InvokeAsync<ContentPartFieldDefinitionDto>(service, "CreateFieldAsync", "RemoteTextPart", new ContentPartFieldDefinitionDto
            {
                Name = "RemoteText",
                FieldName = fieldTypeName,
            }, new ModelStateDictionary());

            var type = await InvokeAsync<ContentTypeDefinitionDto>(service, "CreateTypeAsync", new ContentTypeDefinitionDto
            {
                Name = "RemoteApiType",
                DisplayName = "Remote Api Type",
                Parts =
                [
                    new ContentTypePartDefinitionDto
                    {
                        Name = "RemoteTextPart",
                        PartName = "RemoteTextPart",
                    },
                ],
            }, new ModelStateDictionary());

            Assert.NotNull(part);
            Assert.NotNull(field);
            Assert.NotNull(type);

            part = await InvokeAsync<ContentPartDefinitionDto>(service, "GetPartAsync", "RemoteTextPart");
            type = await InvokeAsync<ContentTypeDefinitionDto>(service, "GetTypeAsync", "RemoteApiType");

            Assert.True(part.Settings.ContentPartSettings.Attachable);
            Assert.True(part.Settings.ContentPartSettings.Reusable);
            Assert.Contains(part.Fields, createdField => createdField.Name == "RemoteText" && createdField.FieldName == fieldTypeName);
            Assert.Contains(type.Parts, createdPart => createdPart.Name == "RemoteTextPart" && createdPart.PartName == "RemoteTextPart");
        });
    }

    private static async Task EnableFeaturesAsync(SiteContext context, params string[] featureIds)
    {
        await context.UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
                .Where(feature => featureIds.Contains(feature.Id, StringComparer.Ordinal))
                .ToArray();

            await shellFeaturesManager.EnableFeaturesAsync(features, force: true);
        });
    }

    private static Type GetServiceType(string fullName, string assemblyName)
        => Type.GetType($"{fullName}, {assemblyName}", throwOnError: true)!;

    private static T Invoke<T>(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);

        return (T)method.Invoke(target, arguments)!;
    }

    private static async Task<T> InvokeAsync<T>(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
        var task = (Task)method.Invoke(target, arguments)!;
        await task;

        return (T)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private sealed class DictionarySchema
    {
        public Dictionary<string, string[]> Values { get; set; } = [];
    }
}
