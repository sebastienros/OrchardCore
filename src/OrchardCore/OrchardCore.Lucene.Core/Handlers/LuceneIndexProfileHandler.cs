using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene.Models;

namespace OrchardCore.Lucene.Core.Handlers;

public sealed class LuceneIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => PopulateAsync(context.Model, context.Data);

    private static Task PopulateAsync(IndexProfile index, JsonNode data)
    {
        if (!CanHandle(index))
        {
            return Task.CompletedTask;
        }

        var LuceneMetadata = index.GetOrCreate<LuceneIndexMetadata>();

        var analyzerName = data[nameof(LuceneMetadata.AnalyzerName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(analyzerName))
        {
            LuceneMetadata.AnalyzerName = analyzerName;
        }

        var storeSourceData = data[nameof(LuceneMetadata.StoreSourceData)]?.GetValue<bool>();

        if (storeSourceData.HasValue)
        {
            LuceneMetadata.StoreSourceData = storeSourceData.Value;
        }

        index.Put(LuceneMetadata);

        return Task.CompletedTask;
    }

    private static bool CanHandle(IndexProfile index)
    {
        return string.Equals(LuceneConstants.ProviderName, index.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
