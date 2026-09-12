using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Tenants.Models;

namespace OrchardCore.Tenants.Services;

public class FeatureProfilesManager
{
    private readonly IDocumentManager<FeatureProfilesDocument> _documentManager;

    public FeatureProfilesManager(IDocumentManager<FeatureProfilesDocument> documentManager) => _documentManager = documentManager;

    /// <summary>
    /// Loads the feature profiles document from the store for updating and that should not be cached.
    /// </summary>
    public Task<FeatureProfilesDocument> LoadFeatureProfilesDocumentAsync() => _documentManager.GetOrCreateMutableAsync();

    /// <summary>
    /// Gets the feature profiles document from the cache for sharing and that should not be updated.
    /// </summary>
    public Task<FeatureProfilesDocument> GetFeatureProfilesDocumentAsync() => _documentManager.GetOrCreateImmutableAsync();

    /// <summary>Removes a stored profile, without saving when it is already absent.</summary>
    public async Task RemoveFeatureProfileAsync(string id)
    {
        var document = await LoadFeatureProfilesDocumentAsync();
        if (document.FeatureProfiles.Remove(id))
        {
            await _documentManager.UpdateAsync(document);
        }
    }

    /// <summary>Updates a profile, preserving rule order and skipping equivalent definitions.</summary>
    public async Task UpdateFeatureProfileAsync(string id, FeatureProfile profile)
    {
        var document = await LoadFeatureProfilesDocumentAsync();
        if (document.FeatureProfiles.TryGetValue(id, out var current) && AreEquivalent(id, current, profile))
        {
            return;
        }

        document.FeatureProfiles[id] = profile;
        await _documentManager.UpdateAsync(document);
    }
    internal static bool AreEquivalent(string id, FeatureProfile left, FeatureProfile right) =>
        left is not null && right is not null &&
        string.Equals(left.Id ?? id, right.Id ?? id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Name ?? id, right.Name ?? id, StringComparison.Ordinal) &&
        left.FeatureRules is not null && right.FeatureRules is not null &&
        left.FeatureRules.Count == right.FeatureRules.Count &&
        left.FeatureRules.Zip(right.FeatureRules).All(pair =>
            pair.First is not null && pair.Second is not null &&
            string.Equals(pair.First.Rule, pair.Second.Rule, StringComparison.Ordinal) &&
            string.Equals(pair.First.Expression, pair.Second.Expression, StringComparison.Ordinal));
}
