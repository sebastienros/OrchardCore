using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OrchardCore.Rules.Services;

/// <summary>
/// Converts supported rule conditions to a transport-independent management contract.
/// Validation never evaluates a condition or persists a document.
/// </summary>
public interface IRuleManagementService
{
    IReadOnlyList<RuleConditionDescriptor> GetDescriptors();
    IReadOnlyList<RuleConditionDefinition> Describe(Rule rule);
    RuleManagementResult CreateRule(IReadOnlyList<RuleConditionDefinition> conditions, string ruleId = null);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RuleConditionDefinition
{
    public string Name { get; init; }
    public string ConditionId { get; init; }
    public JsonObject Properties { get; init; } = [];
    public IReadOnlyList<RuleConditionDefinition> Conditions { get; init; } = [];
}

public sealed class RuleConditionDescriptor
{
    public string Name { get; init; }
    public bool CanWrite { get; init; }
    public bool SupportsChildren { get; init; }
    public JsonObject PropertiesSchema { get; init; }
}

public sealed class RuleManagementResult
{
    public Rule Rule { get; init; }
    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
    public bool IsValid => Errors.Count == 0;
}
