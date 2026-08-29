using System.ComponentModel;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Json;
using OrchardCore.RemoteManagement;
using YesSql;
using YesSql.Services;

namespace OrchardCore.Contents.Services;

internal sealed class ContentApiService
{
    private const int AuthorizationBatchSize = 100;

    private static readonly JsonMergeSettings s_updateJsonMergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private static readonly string[] s_wellKnownContentItemProperties =
    [
        nameof(ContentItem.ContentItemId),
        nameof(ContentItem.ContentItemVersionId),
        nameof(ContentItem.ContentType),
        nameof(ContentItem.DisplayText),
        nameof(ContentItem.Latest),
        nameof(ContentItem.Published),
        nameof(ContentItem.ModifiedUtc),
        nameof(ContentItem.PublishedUtc),
        nameof(ContentItem.CreatedUtc),
        nameof(ContentItem.Owner),
        nameof(ContentItem.Author),
    ];

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly DocumentJsonSerializerOptions _serializerOptions;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IDisplayHelper _displayHelper;
    private readonly ContentOptions _contentOptions;

    public ContentApiService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        IOptions<DocumentJsonSerializerOptions> serializerOptions,
        IContentItemDisplayManager contentItemDisplayManager,
        IDisplayHelper displayHelper,
        IOptions<ContentOptions> contentOptions)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _serializerOptions = serializerOptions.Value;
        _contentItemDisplayManager = contentItemDisplayManager;
        _displayHelper = displayHelper;
        _contentOptions = contentOptions.Value;
    }

    public JsonSerializerOptions SerializerOptions => _serializerOptions.SerializerOptions;

    public async Task<ContentItemsResponse> ListAsync(ClaimsPrincipal user, string contentType, string status, int skip, int take)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(skip, 0);

        var typeDefinitions = string.IsNullOrWhiteSpace(contentType)
            ? await _contentDefinitionManager.ListTypeDefinitionsAsync()
            : [await _contentDefinitionManager.LoadTypeDefinitionAsync(contentType)];

        typeDefinitions = typeDefinitions.Where(x => x is not null).ToArray();

        var allowedTypes = new List<string>();
        foreach (var definition in typeDefinitions)
        {
            if (await _authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.ListContent, definition.Name))
            {
                allowedTypes.Add(definition.Name);
            }
        }

        var response = new ContentItemsResponse { Skip = skip, Take = take };
        if (allowedTypes.Count == 0)
        {
            return response;
        }

        async Task<IEnumerable<ContentItem>> LoadBatchAsync(int offset)
        {
            var query = _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(allowedTypes.ToArray()));
            query = status?.ToLowerInvariant() switch
            {
                "draft" => query.Where(index => index.Latest && !index.Published),
                "latest" => query.Where(index => index.Latest),
                _ => query.Where(index => index.Published),
            };

            return await query.OrderByDescending(index => index.ModifiedUtc)
                .ThenBy(index => index.Id)
                .Skip(offset)
                .Take(AuthorizationBatchSize)
                .ListAsync();
        }

        var authorizedItems = new List<ContentItem>(take);
        var authorizedCount = 0;
        var offset = 0;

        while (true)
        {
            var batch = (await LoadBatchAsync(offset)).ToArray();
            foreach (var contentItem in batch)
            {
                if (!await CanViewContentItemAsync(_authorizationService, user, contentItem))
                {
                    continue;
                }

                if (authorizedCount >= skip && authorizedItems.Count < take)
                {
                    authorizedItems.Add(contentItem);
                }

                authorizedCount++;
            }

            if (batch.Length < AuthorizationBatchSize)
            {
                break;
            }

            offset += batch.Length;
        }

        response.TotalCount = authorizedCount;
        response.Items = authorizedItems;

        return response;
    }

    public async Task<ContentItem> GetAsync(string contentItemId, string version)
    {
        var options = GetVersionOptions(version, defaultToPublished: true);
        return await _contentManager.GetAsync(contentItemId, options);
    }

    public async Task<ContentItem> SaveAsync(ClaimsPrincipal user, ContentItem model, bool publish, bool allowCreate, bool allowUpdate, string contentItemId = null)
    {
        if (model is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(contentItemId))
        {
            model.ContentItemId = contentItemId;
        }

        var modelState = _updateModelAccessor.ModelUpdater.ModelState;
        var contentItem = string.IsNullOrWhiteSpace(model.ContentItemId)
            ? null
            : await _contentManager.GetAsync(model.ContentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            if (!allowCreate || string.IsNullOrWhiteSpace(model.ContentType) || await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType) is null)
            {
                return null;
            }

            contentItem = await _contentManager.NewAsync(model.ContentType);
            if (!string.IsNullOrWhiteSpace(model.ContentItemId))
            {
                contentItem.ContentItemId = model.ContentItemId;
            }

            if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem)
                || (publish && !await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem)))
            {
                return modelState.IsValid ? new ContentItem { ContentItemId = string.Empty } : null;
            }

            if (!await PrepareOwnershipAsync(_authorizationService, user, model, contentItem))
            {
                return modelState.IsValid ? new ContentItem { ContentItemId = string.Empty } : null;
            }

            contentItem.Merge(model);
            var validationResult = await _contentManager.ValidateAsync(contentItem);
            if (!validationResult.Succeeded)
            {
                AddValidationErrorsToModelState(validationResult, modelState);
                await _session.CancelAsync();
                return contentItem;
            }

            await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(contentItemId) && !string.Equals(model.ContentItemId, contentItemId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.ContentItemId))
            {
                modelState.AddModelError(nameof(ContentItem.ContentItemId), "The content item id in the request body must match the route value.");
                return contentItem;
            }

            if (!string.IsNullOrWhiteSpace(model.ContentType) && !string.Equals(model.ContentType, contentItem.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                modelState.AddModelError(nameof(ContentItem.ContentType), "The content type cannot be changed.");
                return contentItem;
            }

            if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem)
                || (publish && !await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem)))
            {
                return new ContentItem { ContentItemId = string.Empty };
            }

            if (!await PrepareOwnershipAsync(_authorizationService, user, model, contentItem))
            {
                return new ContentItem { ContentItemId = string.Empty };
            }

            if (!allowUpdate)
            {
                if (!MatchesRequestedContent(contentItem, model))
                {
                    modelState.AddModelError(nameof(ContentItem.ContentItemId), "A content item with this id already exists with different content.");
                }

                return contentItem;
            }

            if (contentItem.Published == publish && MatchesRequestedContent(contentItem, model))
            {
                return contentItem;
            }

            contentItem = await _contentManager.GetAsync(model.ContentItemId, VersionOptions.DraftRequired);
            contentItem.Merge(model, s_updateJsonMergeSettings);
            await _contentManager.UpdateAsync(contentItem);

            var validationResult = await _contentManager.ValidateAsync(contentItem);
            if (!validationResult.Succeeded)
            {
                AddValidationErrorsToModelState(validationResult, modelState);
                await _session.CancelAsync();
                return contentItem;
            }
        }

        if (publish)
        {
            await _contentManager.PublishAsync(contentItem);
        }
        else
        {
            await _contentManager.SaveDraftAsync(contentItem);
        }

        return contentItem;
    }

    public async Task<ContentItemValidationResponse> ValidateAsync(ClaimsPrincipal user, ContentItem model, string contentItemId = null)
    {
        if (model is null)
        {
            return null;
        }

        var modelState = new ModelStateDictionary();
        ContentItem contentItem;

        if (!string.IsNullOrWhiteSpace(contentItemId) || !string.IsNullOrWhiteSpace(model.ContentItemId))
        {
            var id = contentItemId ?? model.ContentItemId;
            contentItem = await _contentManager.GetAsync(id, VersionOptions.Latest);
            if (contentItem is null)
            {
                return null;
            }

            if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem))
            {
                return new ContentItemValidationResponse();
            }

            if (!await PrepareOwnershipAsync(_authorizationService, user, model, contentItem))
            {
                return new ContentItemValidationResponse();
            }

            contentItem = await _contentManager.GetAsync(id, VersionOptions.DraftRequired);
            contentItem.Merge(model, s_updateJsonMergeSettings);
            await _contentManager.UpdateAsync(contentItem);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(model.ContentType) || await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType) is null)
            {
                return null;
            }

            contentItem = await _contentManager.NewAsync(model.ContentType);
            if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem))
            {
                return new ContentItemValidationResponse();
            }

            if (!await PrepareOwnershipAsync(_authorizationService, user, model, contentItem))
            {
                return new ContentItemValidationResponse();
            }

            contentItem.Merge(model);
        }

        var validationResult = await _contentManager.ValidateAsync(contentItem);
        if (!validationResult.Succeeded)
        {
            AddValidationErrorsToModelState(validationResult, modelState);
        }

        await _session.CancelAsync();

        return new ContentItemValidationResponse
        {
            IsValid = modelState.IsValid,
            Errors = modelState.ToDictionary(entry => entry.Key, entry => entry.Value.Errors.Select(x => x.ErrorMessage).ToArray()),
        };
    }

    internal static Task<bool> CanViewContentItemAsync(
        IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        ContentItem contentItem)
    {
        var permission = contentItem.Published ? CommonPermissions.ViewContent : CommonPermissions.PreviewContent;
        return authorizationService.AuthorizeAsync(user, permission, contentItem);
    }

    internal static async Task<bool> PrepareOwnershipAsync(
        IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        ContentItem model,
        ContentItem contentItem)
    {
        model.Author = contentItem.Author;

        if (string.IsNullOrEmpty(model.Owner))
        {
            model.Owner = contentItem.Owner;
            return true;
        }

        return string.Equals(model.Owner, contentItem.Owner, StringComparison.Ordinal) ||
            await authorizationService.AuthorizeAsync(user, CommonPermissions.EditContentOwner, contentItem);
    }

    internal static bool MatchesRequestedContent(ContentItem contentItem, ContentItem model)
    {
        if (!string.IsNullOrWhiteSpace(model.ContentType) &&
            !string.Equals(contentItem.ContentType, model.ContentType, StringComparison.Ordinal))
        {
            return false;
        }

        if (model.DisplayText is not null &&
            !string.Equals(contentItem.DisplayText, model.DisplayText, StringComparison.Ordinal))
        {
            return false;
        }

        if (model.Owner is not null &&
            !string.Equals(contentItem.Owner, model.Owner, StringComparison.Ordinal))
        {
            return false;
        }

        var requestedContent = JsonSerializer.SerializeToNode(model)?.AsObject() ?? [];
        var existingContent = JsonSerializer.SerializeToNode(contentItem)?.AsObject() ?? [];
        foreach (var propertyName in s_wellKnownContentItemProperties)
        {
            requestedContent.Remove(propertyName);
            existingContent.Remove(propertyName);
        }

        foreach (var property in requestedContent)
        {
            if (!JsonNode.DeepEquals(existingContent[property.Key], property.Value))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<ContentItem> CreateDraftAsync(ClaimsPrincipal user, string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (contentItem is null)
        {
            return null;
        }

        if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem))
        {
            return new ContentItem { ContentItemId = string.Empty };
        }

        await _contentManager.SaveDraftAsync(contentItem);
        return await _contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
    }

    public async Task<ContentItem> PublishAsync(ClaimsPrincipal user, string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Draft);
        if (contentItem is null)
        {
            contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
            if (contentItem is null)
            {
                return null;
            }

            return await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem)
                ? contentItem
                : new ContentItem { ContentItemId = string.Empty };
        }

        if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem))
        {
            return new ContentItem { ContentItemId = string.Empty };
        }

        await _contentManager.PublishAsync(contentItem);
        return contentItem;
    }

    public async Task<ContentItem> UnpublishAsync(ClaimsPrincipal user, string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
        if (contentItem is null)
        {
            contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);
            if (contentItem is null)
            {
                return null;
            }

            return await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem)
                ? contentItem
                : new ContentItem { ContentItemId = string.Empty };
        }

        if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.PublishContent, contentItem))
        {
            return new ContentItem { ContentItemId = string.Empty };
        }

        await _contentManager.UnpublishAsync(contentItem);
        return await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);
    }

    public async Task<ContentItem> DeleteAsync(ClaimsPrincipal user, string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (contentItem is null)
        {
            return null;
        }

        if (!await _authorizationService.AuthorizeAsync(user, CommonPermissions.DeleteContent, contentItem))
        {
            return new ContentItem { ContentItemId = string.Empty };
        }

        await _contentManager.RemoveAsync(contentItem);
        return contentItem;
    }

    public async Task<ContentItemRenderResponse> RenderAsync(ClaimsPrincipal user, string contentItemId, string version, string displayType)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, GetVersionOptions(version, defaultToPublished: true));
        if (contentItem is null)
        {
            return null;
        }

        var permission = contentItem.Published ? CommonPermissions.ViewContent : CommonPermissions.PreviewContent;
        if (!await _authorizationService.AuthorizeAsync(user, permission, contentItem))
        {
            return new ContentItemRenderResponse { ContentItemId = string.Empty };
        }

        var shape = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, _updateModelAccessor.ModelUpdater, displayType ?? "Detail");
        var html = await _displayHelper.ShapeExecuteAsync(shape);

        using var writer = new StringWriter();
        html.WriteTo(writer, HtmlEncoder.Default);

        return new ContentItemRenderResponse
        {
            ContentItemId = contentItem.ContentItemId,
            DisplayType = displayType ?? "Detail",
            Html = writer.ToString(),
        };
    }

    public async Task<ContentItemSchemaResponse> GetSchemaAsync(string contentType)
    {
        var definition = await _contentDefinitionManager.LoadTypeDefinitionAsync(contentType);
        if (definition is null)
        {
            return null;
        }

        return new ContentItemSchemaResponse
        {
            ContentType = contentType,
            Schema = ContentItemSchemaBuilder.BuildSchema(definition, _contentOptions, _serializerOptions.SerializerOptions),
        };
    }

    public static bool IsForbidden(ContentItem contentItem)
        => contentItem is not null && contentItem.ContentItemId == string.Empty;

    public static bool IsForbidden(ContentItemValidationResponse response)
        => response is not null && !response.IsValid && response.Errors.Count == 0;

    public static bool IsForbidden(ContentItemRenderResponse response)
        => response is not null && response.ContentItemId == string.Empty;

    private static VersionOptions GetVersionOptions(string version, bool defaultToPublished)
        => version?.ToLowerInvariant() switch
        {
            "draft" => VersionOptions.DraftRequired,
            "latest" => VersionOptions.Latest,
            _ => defaultToPublished ? VersionOptions.Published : VersionOptions.Latest,
        };

    private static void AddValidationErrorsToModelState(ContentValidateResult result, ModelStateDictionary modelState)
    {
        foreach (var error in result.Errors)
        {
            if (error.MemberNames != null && error.MemberNames.Any())
            {
                foreach (var memberName in error.MemberNames)
                {
                    modelState.AddModelError(memberName, error.ErrorMessage);
                }
            }
            else
            {
                modelState.AddModelError(string.Empty, error.ErrorMessage);
            }
        }
    }
}

internal sealed class ContentRemoteManagementCapabilityProvider : IRemoteManagementCapabilityProvider
{
    public ValueTask<IEnumerable<RemoteManagementCapability>> GetCapabilitiesAsync() =>
        ValueTask.FromResult<IEnumerable<RemoteManagementCapability>>(
            [
                new RemoteManagementCapability
                {
                    Id = "content-items",
                    Version = $"{RemoteManagementConstants.ProtocolMajorVersion}.{RemoteManagementConstants.ProtocolMinorVersion}",
                    DisplayName = "Content Items",
                },
            ]);
}

internal static class ContentItemSchemaBuilder
{
    public static JsonObject BuildSchema(
        ContentTypeDefinition definition,
        ContentOptions contentOptions,
        JsonSerializerOptions serializerOptions)
    {
        var schema = GetJsonSchema(typeof(ContentItemSchema), serializerOptions);
        var properties = (JsonObject)schema["properties"];

        foreach (var part in definition.Parts)
        {
            var partSchema = CreateContentPartSchema(part.PartDefinition, contentOptions, serializerOptions);
            RebaseLocalReferences(partSchema, $"/properties/{EscapeJsonPointerSegment(part.Name)}");
            properties[part.Name] = partSchema;
        }

        var contentTypeProperty = GetJsonPropertyName(nameof(ContentItemSchema.ContentType), serializerOptions);
        properties[contentTypeProperty]["const"] = definition.Name;
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.ContentItemId), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.ContentItemVersionId), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.Published), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.Latest), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.ModifiedUtc), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.PublishedUtc), serializerOptions));
        SetReadOnly(properties, GetJsonPropertyName(nameof(ContentItemSchema.CreatedUtc), serializerOptions));

        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["title"] = definition.DisplayName;
        schema["required"] = new JsonArray(contentTypeProperty);
        schema["additionalProperties"] = true;

        return schema;
    }

    private static JsonObject CreateContentPartSchema(
        ContentPartDefinition definition,
        ContentOptions contentOptions,
        JsonSerializerOptions serializerOptions)
    {
        var schema = GetJsonSchema(GetContentPartType(definition.Name, contentOptions), serializerOptions);
        var properties = schema["properties"] as JsonObject;

        if (properties is null)
        {
            properties = [];
            schema["properties"] = properties;
        }

        foreach (var field in definition.Fields)
        {
            var fieldSchema = GetJsonSchema(
                GetContentFieldType(field.FieldDefinition.Name, contentOptions),
                serializerOptions);
            RebaseLocalReferences(
                fieldSchema,
                $"/properties/{EscapeJsonPointerSegment(field.Name)}");
            properties[field.Name] = fieldSchema;
        }

        return schema;
    }

    private static JsonObject GetJsonSchema(Type type, JsonSerializerOptions serializerOptions)
    {
        var schema = serializerOptions.GetJsonSchemaAsNode(
            type,
            new JsonSchemaExporterOptions
            {
                TransformSchemaNode = static (context, node) =>
                {
                    if (context.PropertyInfo?.AttributeProvider is MemberInfo member &&
                        member.GetCustomAttribute<DescriptionAttribute>() is { } description)
                    {
                        var transformed = node as JsonObject ?? [];
                        transformed["description"] = description.Description;

                        return transformed;
                    }

                    return node;
                },
            });

        return schema as JsonObject ?? [];
    }

    private static void RebaseLocalReferences(JsonNode node, string prefix)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject["$ref"]?.GetValue<string>() is { } reference &&
                reference.StartsWith('#'))
            {
                jsonObject["$ref"] = $"#{prefix}{reference[1..]}";
            }

            foreach (var property in jsonObject)
            {
                if (property.Value is not null)
                {
                    RebaseLocalReferences(property.Value, prefix);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item is not null)
                {
                    RebaseLocalReferences(item, prefix);
                }
            }
        }
    }

    private static string EscapeJsonPointerSegment(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static Type GetContentPartType(string name, ContentOptions contentOptions) =>
        contentOptions.ContentPartOptionsLookup.TryGetValue(name, out var option)
            ? option.Type
            : typeof(ContentPart);

    private static Type GetContentFieldType(string name, ContentOptions contentOptions) =>
        contentOptions.ContentFieldOptionsLookup.TryGetValue(name, out var option)
            ? option.Type
            : typeof(ContentField);

    private static void SetReadOnly(JsonObject properties, string name)
    {
        if (properties[name] is JsonObject property)
        {
            property["readOnly"] = true;
        }
    }

    private static string GetJsonPropertyName(string name, JsonSerializerOptions serializerOptions)
        => serializerOptions.GetTypeInfo(typeof(ContentItemSchema)).Properties
            .First(property => property.AttributeProvider is MemberInfo member && member.Name == name)
            .Name;

    private sealed class ContentItemSchema
    {
        public string ContentItemId { get; set; }

        public string ContentItemVersionId { get; set; }

        public string ContentType { get; set; }

        public string DisplayText { get; set; }

        public bool Latest { get; set; }

        public bool Published { get; set; }

        public DateTime? ModifiedUtc { get; set; }

        public DateTime? PublishedUtc { get; set; }

        public DateTime? CreatedUtc { get; set; }

        public string Owner { get; set; }

        public string Author { get; set; }
    }
}
