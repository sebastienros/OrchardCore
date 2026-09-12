using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Core.Handlers;
using OrchardCore.Lucene.Models;
using OrchardCore.Lucene.Services;
using OrchardCore.Search.Lucene;

namespace OrchardCore.Tests.Modules.OrchardCore.Indexing;

public class LuceneIndexValidationTests
{
    [Theory]
    [InlineData("../other")]
    [InlineData("..\\other")]
    [InlineData("/absolute")]
    [InlineData("C:drive")]
    [InlineData("..")]
    [InlineData("index.")]
    [InlineData(" trailing ")]
    public async Task UnsafeProviderNames_AreRejectedBeforeStoreLookup(string name)
    {
        var store = new Mock<IIndexProfileStore>(MockBehavior.Strict);
        var profile = Profile(name);
        var context = new ValidatingContext<IndexProfile>(profile);

        await Handler(store.Object).ValidatingAsync(context);

        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(IndexProfile.IndexName)));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownIndexAndQueryAnalyzers_AreRejectedWithMemberNames()
    {
        var profile = Profile("articles");
        profile.Put(new LuceneIndexMetadata { AnalyzerName = "missing-index-analyzer" });
        profile.Put(new LuceneIndexDefaultQueryMetadata { QueryAnalyzerName = "missing-query-analyzer" });
        var context = new ValidatingContext<IndexProfile>(profile);

        await Handler(Mock.Of<IIndexProfileStore>()).ValidatingAsync(context);

        Assert.Equal(2, context.Result.Errors.Count);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains("AnalyzerName"));
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains("QueryAnalyzerName"));
    }

    [Theory]
    [InlineData("another", false)]
    [InlineData("id", true)]
    public async Task ProviderNameUniqueness_AllowsTheSameProfile(string existingId, bool valid)
    {
        var profile = Profile("articles");
        var store = new Mock<IIndexProfileStore>();
        store.Setup(value => value.FindByIndexNameAndProviderAsync("articles", "Lucene")).ReturnsAsync(new IndexProfile { Id = existingId });
        var context = new ValidatingContext<IndexProfile>(profile);

        await Handler(store.Object).ValidatingAsync(context);

        Assert.Equal(valid, context.Result.Succeeded);
    }

    [Fact]
    public async Task ShippedBlogRecipeCompatibilityVersion_RemainsValid()
    {
        var profile = Profile("Search");
        profile.Put(new LuceneIndexDefaultQueryMetadata
        {
            DefaultVersion = Enum.Parse<global::Lucene.Net.Util.LuceneVersion>("LUCENE_30"),
        });
        var context = new ValidatingContext<IndexProfile>(profile);

        await Handler(Mock.Of<IIndexProfileStore>()).ValidatingAsync(context);

        Assert.True(context.Result.Succeeded);
    }

    private static IndexProfile Profile(string name) => new()
    {
        Id = "id", ProviderName = "Lucene", Type = "Content", IndexName = name, IndexFullName = name,
    };

    private static LuceneIndexValidationHandler Handler(IIndexProfileStore store)
    {
        var options = new LuceneOptions();
        options.Analyzers.Add(new LuceneAnalyzer(LuceneConstants.DefaultAnalyzer, () => throw new InvalidOperationException("Validation must not instantiate analyzers.")));
        var localizer = new Mock<IStringLocalizer<LuceneIndexValidationHandler>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string message) => new LocalizedString(message, message));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string message, object[] arguments) => new LocalizedString(message, string.Format(message, arguments)));
        return new LuceneIndexValidationHandler(store, new LuceneAnalyzerManager(Options.Create(options)), localizer.Object);
    }
}
