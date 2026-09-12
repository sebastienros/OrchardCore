using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene.Models;
using OrchardCore.Lucene.Services;

namespace OrchardCore.Lucene.Core.Handlers;

/// <summary>
/// Validates Lucene definitions shared by administration, recipes and remote management.
/// </summary>
public sealed class LuceneIndexValidationHandler : IndexProfileHandlerBase
{
    private readonly IIndexProfileStore _store;
    private readonly LuceneAnalyzerManager _analyzers;
    private readonly IStringLocalizer S;

    /// <summary>Creates validation using the tenant's stored profiles and registered analyzers.</summary>
    public LuceneIndexValidationHandler(IIndexProfileStore store, LuceneAnalyzerManager analyzers, IStringLocalizer<LuceneIndexValidationHandler> localizer)
    {
        _store = store;
        _analyzers = analyzers;
        S = localizer;
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<IndexProfile> context)
    {
        var profile = context.Model;
        if (!string.Equals(profile.ProviderName, LuceneConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!LuceneIndexNameValidator.IsValid(profile.IndexName) || !LuceneIndexNameValidator.IsValid(profile.IndexFullName))
        {
            context.Result.Fail(new ValidationResult(S["The Lucene index name must be a single file name without path separators or reserved filename characters."], [nameof(IndexProfile.IndexName)]));
        }
        else
        {
            var existing = await _store.FindByIndexNameAndProviderAsync(profile.IndexName, profile.ProviderName);
            if (existing is not null && existing.Id != profile.Id)
            {
                context.Result.Fail(new ValidationResult(S["There is already another index with the same name."], [nameof(IndexProfile.IndexName)]));
            }
        }

        var metadata = profile.GetOrCreate<LuceneIndexMetadata>();
        var query = profile.GetOrCreate<LuceneIndexDefaultQueryMetadata>();
        ValidateAnalyzer(metadata.AnalyzerName, nameof(metadata.AnalyzerName), context);
        ValidateAnalyzer(query.QueryAnalyzerName, nameof(query.QueryAnalyzerName), context);
        if (!Enum.IsDefined(query.DefaultVersion))
        {
            context.Result.Fail(new ValidationResult(S["The Lucene version is not supported."], [nameof(query.DefaultVersion)]));
        }
    }

    private void ValidateAnalyzer(string name, string member, ValidatingContext<IndexProfile> context)
    {
        var effectiveName = string.IsNullOrEmpty(name) ? LuceneConstants.DefaultAnalyzer : name;
        if (!_analyzers.GetAnalyzers().Any(analyzer => string.Equals(analyzer.Name, effectiveName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Result.Fail(new ValidationResult(S["The Lucene analyzer '{0}' is not registered.", effectiveName], [member]));
        }
    }

}
