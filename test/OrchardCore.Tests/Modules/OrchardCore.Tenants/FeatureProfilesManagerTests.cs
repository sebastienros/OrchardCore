using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Tenants.Models;
using OrchardCore.Tenants.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Tenants;

public class FeatureProfilesManagerTests
{
    [Fact]
    public async Task Update_EquivalentDefinition_DoesNotPersistAgain()
    {
        var document = new FeatureProfilesDocument();
        document.FeatureProfiles["profile"] = Definition();
        var manager = CreateManager(document, out var documents);

        await manager.UpdateFeatureProfileAsync("profile", Definition());

        documents.Verify(store => store.UpdateAsync(It.IsAny<FeatureProfilesDocument>(),
            It.IsAny<Func<FeatureProfilesDocument, Task>>()), Times.Never);
    }

    [Fact]
    public async Task Remove_MissingProfile_DoesNotPersistAgain()
    {
        var manager = CreateManager(new FeatureProfilesDocument(), out var documents);

        await manager.RemoveFeatureProfileAsync("missing");

        documents.Verify(store => store.UpdateAsync(It.IsAny<FeatureProfilesDocument>(),
            It.IsAny<Func<FeatureProfilesDocument, Task>>()), Times.Never);
    }

    [Fact]
    public async Task Update_ChangedRuleOrder_PersistsNewPrecedence()
    {
        var document = new FeatureProfilesDocument();
        document.FeatureProfiles["profile"] = Definition();
        var manager = CreateManager(document, out var documents);
        var replacement = Definition();
        replacement.FeatureRules.Reverse();

        await manager.UpdateFeatureProfileAsync("profile", replacement);

        Assert.Equal("Include", document.FeatureProfiles["profile"].FeatureRules[0].Rule);
        documents.Verify(store => store.UpdateAsync(document,
            It.IsAny<Func<FeatureProfilesDocument, Task>>()), Times.Once);
    }

    private static FeatureProfile Definition() => new()
    {
        Id = "profile", Name = "Profile",
        FeatureRules = [new() { Rule = "Exclude", Expression = "Custom.*" },
            new() { Rule = "Include", Expression = "Custom.Allowed" }],
    };

    private static FeatureProfilesManager CreateManager(FeatureProfilesDocument document,
        out Mock<IDocumentManager<FeatureProfilesDocument>> documents)
    {
        documents = new Mock<IDocumentManager<FeatureProfilesDocument>>();
        documents.Setup(store => store.GetOrCreateMutableAsync(It.IsAny<Func<Task<FeatureProfilesDocument>>>())).ReturnsAsync(document);
        documents.Setup(store => store.UpdateAsync(document, It.IsAny<Func<FeatureProfilesDocument, Task>>())).Returns(Task.CompletedTask);
        return new FeatureProfilesManager(documents.Object);
    }
}
