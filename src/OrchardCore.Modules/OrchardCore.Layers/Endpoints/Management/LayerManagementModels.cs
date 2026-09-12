using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrchardCore.Rules.Services;

namespace OrchardCore.Layers.Endpoints.Management;

/// <summary>
/// A complete layer definition. Conditions replace the entire existing rule.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LayerDefinitionDto
{
    [Required]
    [MaxLength(256)]
    public string Name { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<RuleConditionDefinition> Conditions { get; init; } = [];
}

public sealed class LayerListRequest
{
    public string Search { get; init; }
    [Range(0, int.MaxValue)]
    public int? Skip { get; init; }
    [Range(1, 200)]
    public int? Take { get; init; }
}

public sealed class LayerListResponse
{
    public int Skip { get; init; }
    public int Take { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<LayerDefinitionDto> Items { get; init; } = [];
}
