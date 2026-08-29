using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Contents.Services;
using OrchardCore.Json;
using OrchardCore.Settings;
using YesSql;

namespace OrchardCore.CustomSettings.Services;

internal sealed class CustomSettingsManagementService
{
    private static readonly HashSet<string> s_forbiddenProperties = new(StringComparer.Ordinal)
    {
        nameof(ContentItem.ContentItemId),
        nameof(ContentItem.ContentItemVersionId),
        nameof(ContentItem.Id),
        nameof(ContentItem.Latest),
        nameof(ContentItem.Published),
        nameof(ContentItem.ModifiedUtc),
        nameof(ContentItem.PublishedUtc),
        nameof(ContentItem.CreatedUtc),
        nameof(ContentItem.Owner),
        nameof(ContentItem.Author),
    };

    private static readonly JsonMergeSettings s_mergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
        MergeNullValueHandling = MergeNullValueHandling.Merge,
    };

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISiteService _siteService;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ContentOptions _contentOptions;
    private readonly JsonSerializerOptions _serializerOptions;

    public CustomSettingsManagementService(
        IContentDefinitionManager contentDefinitionManager,
        ISiteService siteService,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        ISession session,
        IOptions<ContentOptions> contentOptions,
        IOptions<DocumentJsonSerializerOptions> serializerOptions)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _siteService = siteService;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _session = session;
        _contentOptions = contentOptions.Value;
        _serializerOptions = serializerOptions.Value.SerializerOptions;
    }

    public JsonSerializerOptions SerializerOptions => _serializerOptions;

    public async Task<CustomSettingsListResponse> ListAsync(
        ClaimsPrincipal user,
        string search,
        int skip,
        int take)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var authorized = new List<CustomSettingsMetadata>();

        foreach (var definition in definitions.Where(IsCustomSettings))
        {
            if (!await IsAuthorizedAsync(user, definition))
            {
                continue;
            }

            var description = definition.GetSettings<ContentTypeSettings>().Description;
            if (!MatchesSearch(definition, description, search))
            {
                continue;
            }

            authorized.Add(new CustomSettingsMetadata
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Description = description,
            });
        }

        var ordered = authorized
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

        return new CustomSettingsListResponse
        {
            Skip = skip,
            Take = take,
            TotalCount = ordered.Length,
            Items = ordered.Skip(skip).Take(take).ToArray(),
        };
    }

    public async Task<CustomSettingsManagementResult<JsonObject>> GetAsync(
        ClaimsPrincipal user,
        string name)
    {
        var definitionResult = await GetAuthorizedDefinitionAsync(user, name);
        if (!definitionResult.Succeeded)
        {
            return CustomSettingsManagementResult<JsonObject>.FromStatus(definitionResult.Status);
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var contentItem = await GetContentItemAsync(site, definitionResult.Value);

        return CustomSettingsManagementResult<JsonObject>.Success(
            CreateSafeEnvelope(contentItem, definitionResult.Value));
    }

    public async Task<CustomSettingsManagementResult<JsonObject>> UpdateAsync(
        ClaimsPrincipal user,
        string name,
        JsonObject input)
    {
        var definitionResult = await GetAuthorizedDefinitionAsync(user, name);
        if (!definitionResult.Succeeded)
        {
            return CustomSettingsManagementResult<JsonObject>.FromStatus(definitionResult.Status);
        }

        var definition = definitionResult.Value;
        var canonicalName = definition.Name;
        var errors = ValidateInput(canonicalName, definition, input);
        if (errors.Count > 0)
        {
            return CustomSettingsManagementResult<JsonObject>.ValidationFailure(errors);
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var contentItem = await GetContentItemAsync(site, definition);
        contentItem.Merge(input, s_mergeSettings);
        contentItem.ContentType = canonicalName;

        var envelope = CreateSafeEnvelope(contentItem, definition);
        errors = ValidateEnvelope(definition, envelope);
        if (errors.Count > 0)
        {
            return CustomSettingsManagementResult<JsonObject>.ValidationFailure(errors);
        }

        await _contentManager.UpdateAsync(contentItem);
        var validationResult = await _contentManager.ValidateAsync(contentItem);
        if (!validationResult.Succeeded)
        {
            await _session.CancelAsync();
            return CustomSettingsManagementResult<JsonObject>.ValidationFailure(
                CreateValidationErrors(validationResult));
        }

        envelope = CreateSafeEnvelope(contentItem, definition);
        site.Properties[canonicalName] = envelope.DeepClone();
        await _siteService.UpdateSiteSettingsAsync(site);

        // UpdateAsync invokes content handlers but custom settings belong only to the site document.
        _session.Delete(contentItem);

        return CustomSettingsManagementResult<JsonObject>.Success(envelope);
    }

    public async Task<CustomSettingsManagementResult<JsonObject>> GetSchemaAsync(
        ClaimsPrincipal user,
        string name)
    {
        var definitionResult = await GetAuthorizedDefinitionAsync(user, name);
        if (!definitionResult.Succeeded)
        {
            return CustomSettingsManagementResult<JsonObject>.FromStatus(definitionResult.Status);
        }

        return CustomSettingsManagementResult<JsonObject>.Success(
            BuildSchema(definitionResult.Value));
    }

    internal async ValueTask<IEnumerable<SiteSettingsManagementSchemaSection>> GetSchemaSectionsAsync(
        ClaimsPrincipal user)
    {
        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var sections = new List<SiteSettingsManagementSchemaSection>();

        foreach (var definition in definitions.Where(IsCustomSettings))
        {
            if (!await IsAuthorizedAsync(user, definition))
            {
                continue;
            }

            sections.Add(new SiteSettingsManagementSchemaSection
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Description = definition.GetSettings<ContentTypeSettings>().Description,
                Schema = BuildSchema(definition),
            });
        }

        return sections;
    }

    private JsonObject BuildSchema(ContentTypeDefinition definition)
    {
        var schema = ContentItemSchemaBuilder.BuildSchema(definition, _contentOptions, _serializerOptions);
        var properties = schema["properties"]?.AsObject() ?? [];

        foreach (var propertyName in s_forbiddenProperties)
        {
            properties.Remove(propertyName);
        }

        var allowed = GetAllowedProperties(definition);
        foreach (var propertyName in properties.Select(property => property.Key).ToArray())
        {
            if (!allowed.Contains(propertyName))
            {
                properties.Remove(propertyName);
            }
        }

        if (properties[nameof(ContentItem.ContentType)] is JsonObject contentTypeSchema)
        {
            contentTypeSchema["const"] = definition.Name;
        }

        schema["title"] = definition.DisplayName;
        var description = definition.GetSettings<ContentTypeSettings>().Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            schema["description"] = description;
        }
        else
        {
            schema.Remove("description");
        }

        schema["required"] = new JsonArray();
        schema["additionalProperties"] = false;

        return schema;
    }

    private async Task<CustomSettingsManagementResult<ContentTypeDefinition>> GetAuthorizedDefinitionAsync(
        ClaimsPrincipal user,
        string name)
    {
        var definition = await _contentDefinitionManager.LoadTypeDefinitionAsync(name);
        if (!IsCustomSettings(definition))
        {
            return CustomSettingsManagementResult<ContentTypeDefinition>.NotFound();
        }

        return await IsAuthorizedAsync(user, definition)
            ? CustomSettingsManagementResult<ContentTypeDefinition>.Success(definition)
            : CustomSettingsManagementResult<ContentTypeDefinition>.Forbidden();
    }

    private Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, ContentTypeDefinition definition)
        => _authorizationService.AuthorizeAsync(user, Permissions.CreatePermissionForType(definition));

    private async Task<ContentItem> GetContentItemAsync(ISite site, ContentTypeDefinition definition)
    {
        var contentItem = await _contentManager.NewAsync(definition.Name);

        if (site.Properties[definition.Name] is JsonNode stored)
        {
            var existing = stored.Deserialize<ContentItem>(_serializerOptions);
            if (existing is not null)
            {
                contentItem.Merge(existing, s_mergeSettings);
            }
        }

        contentItem.ContentType = definition.Name;

        return contentItem;
    }

    private JsonObject CreateSafeEnvelope(ContentItem contentItem, ContentTypeDefinition definition)
    {
        var serialized = JsonSerializer.SerializeToNode(contentItem, _serializerOptions)?.AsObject() ?? [];
        var allowed = GetAllowedProperties(definition);

        foreach (var propertyName in serialized.Select(property => property.Key).ToArray())
        {
            if (!allowed.Contains(propertyName))
            {
                serialized.Remove(propertyName);
            }
        }

        serialized[nameof(ContentItem.ContentType)] = definition.Name;

        return serialized;
    }

    private static Dictionary<string, string[]> ValidateInput(
        string name,
        ContentTypeDefinition definition,
        JsonObject input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (input is null)
        {
            errors[string.Empty] = ["A JSON object payload is required."];
            return errors;
        }

        if (input.TryGetPropertyValue(nameof(ContentItem.ContentType), out var contentTypeNode))
        {
            string contentType;
            try
            {
                contentType = contentTypeNode?.GetValue<string>();
            }
            catch (InvalidOperationException)
            {
                contentType = null;
            }

            if (!string.Equals(contentType, name, StringComparison.Ordinal))
            {
                errors[nameof(ContentItem.ContentType)] =
                    [$"ContentType must exactly match the route name '{name}'."];
            }
        }

        var allowed = GetAllowedProperties(definition);
        foreach (var property in input)
        {
            if (s_forbiddenProperties.Contains(property.Key))
            {
                errors[property.Key] = [$"The '{property.Key}' property cannot be updated."];
            }
            else if (!allowed.Contains(property.Key))
            {
                errors[property.Key] =
                    [$"The '{property.Key}' property is not declared by custom settings type '{name}'."];
            }
        }

        foreach (var error in ValidateEnvelope(definition, input))
        {
            errors.TryAdd(error.Key, error.Value);
        }

        return errors;
    }

    private static Dictionary<string, string[]> CreateValidationErrors(ContentValidateResult result)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in result.Errors)
        {
            var memberNames = error.MemberNames?.Any() == true
                ? error.MemberNames
                : [string.Empty];

            foreach (var memberName in memberNames)
            {
                if (!errors.TryGetValue(memberName, out var messages))
                {
                    messages = [];
                    errors[memberName] = messages;
                }

                messages.Add(error.ErrorMessage);
            }
        }

        return errors.ToDictionary(
            error => error.Key,
            error => error.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> ValidateEnvelope(
        ContentTypeDefinition definition,
        JsonObject envelope)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var part in definition.Parts)
        {
            if (!envelope.TryGetPropertyValue(part.Name, out var partNode))
            {
                continue;
            }

            if (partNode is not JsonObject partObject)
            {
                errors[part.Name] = [$"The '{part.Name}' property must be a JSON object."];
                continue;
            }

            foreach (var field in part.PartDefinition.Fields)
            {
                if (partObject.TryGetPropertyValue(field.Name, out var fieldNode) &&
                    fieldNode is not JsonObject)
                {
                    errors[$"{part.Name}.{field.Name}"] =
                        [$"The '{part.Name}.{field.Name}' property must be a JSON object."];
                }
            }
        }

        return errors;
    }

    private static HashSet<string> GetAllowedProperties(ContentTypeDefinition definition)
        => new(
            definition.Parts.Select(part => part.Name)
                .Append(nameof(ContentItem.ContentType))
                .Append(nameof(ContentItem.DisplayText)),
            StringComparer.Ordinal);

    private static bool IsCustomSettings(ContentTypeDefinition definition)
        => definition?.StereotypeEquals(CustomSettingsConstants.Stereotype) == true;

    private static bool MatchesSearch(
        ContentTypeDefinition definition,
        string description,
        string search)
        => string.IsNullOrWhiteSpace(search)
            || definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (definition.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
}

internal sealed class CustomSettingsManagementSchemaProvider : ISiteSettingsManagementSchemaProvider
{
    private readonly CustomSettingsManagementService _service;

    public CustomSettingsManagementSchemaProvider(CustomSettingsManagementService service)
    {
        _service = service;
    }

    public ValueTask<IEnumerable<SiteSettingsManagementSchemaSection>> GetSchemaSectionsAsync(
        ClaimsPrincipal user)
        => _service.GetSchemaSectionsAsync(user);
}

internal enum CustomSettingsManagementStatus
{
    Success,
    NotFound,
    Forbidden,
    ValidationFailed,
}

internal sealed class CustomSettingsManagementResult<T>
{
    private CustomSettingsManagementResult(
        CustomSettingsManagementStatus status,
        T value = default,
        Dictionary<string, string[]> errors = null)
    {
        Status = status;
        Value = value;
        Errors = errors ?? [];
    }

    public CustomSettingsManagementStatus Status { get; }

    public T Value { get; }

    public Dictionary<string, string[]> Errors { get; }

    public bool Succeeded => Status == CustomSettingsManagementStatus.Success;

    public static CustomSettingsManagementResult<T> Success(T value)
        => new(CustomSettingsManagementStatus.Success, value);

    public static CustomSettingsManagementResult<T> NotFound()
        => new(CustomSettingsManagementStatus.NotFound);

    public static CustomSettingsManagementResult<T> Forbidden()
        => new(CustomSettingsManagementStatus.Forbidden);

    public static CustomSettingsManagementResult<T> ValidationFailure(Dictionary<string, string[]> errors)
        => new(CustomSettingsManagementStatus.ValidationFailed, errors: errors);

    public static CustomSettingsManagementResult<T> FromStatus(CustomSettingsManagementStatus status)
        => new(status);
}

internal sealed class CustomSettingsMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}

internal sealed class CustomSettingsListResponse
{
    [JsonPropertyName("skip")]
    public int Skip { get; set; }

    [JsonPropertyName("take")]
    public int Take { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<CustomSettingsMetadata> Items { get; set; } = [];
}
