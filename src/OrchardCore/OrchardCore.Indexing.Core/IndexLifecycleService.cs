using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Models;

namespace OrchardCore.Indexing.Core;

/// <summary>Executes index lifecycle work through the shared worker and its per-index lock.</summary>
public interface IIndexLifecycleService
{
    /// <summary>Executes an action and returns its processing outcome. This method does not schedule background work.</summary>
    Task<IndexProcessingResult> ExecuteAsync(string indexId, IndexLifecycleAction action);
}

/// <summary>The requested index lifecycle operation.</summary>
public enum IndexLifecycleAction
{
    /// <summary>Processes tasks after the current provider cursor.</summary>
    Synchronize,
    /// <summary>Resets the cursor and reprocesses tasks without recreating the provider index.</summary>
    Reset,
    /// <summary>Recreates the provider index, resets the cursor and reprocesses tasks.</summary>
    Rebuild,
}

/// <summary>Coordinates reset, rebuild and synchronization without reacquiring the worker's lock.</summary>
public sealed class IndexLifecycleService : IIndexLifecycleService
{
    private readonly IIndexProfileManager _profiles;
    private readonly IServiceProvider _services;

    /// <summary>Creates the coordinator from tenant-local profiles and registered indexing sources.</summary>
    public IndexLifecycleService(IIndexProfileManager profiles, IServiceProvider services)
    {
        _profiles = profiles;
        _services = services;
    }

    /// <inheritdoc />
    public async Task<IndexProcessingResult> ExecuteAsync(string indexId, IndexLifecycleAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexId);
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        var profile = await _profiles.FindByIdAsync(indexId);
        if (profile is null)
        {
            return new IndexProcessingResult { IndexId = indexId, Status = IndexProcessingStatus.NotFound };
        }
        var processor = _services.GetKeyedService<NamedIndexingService>(profile.Type);
        if (processor is null)
        {
            return new IndexProcessingResult { IndexId = indexId, Status = IndexProcessingStatus.Unsupported };
        }
        var result = await processor.ProcessIndexWithPreparationAsync(profile, async (index, provider) =>
        {
            if (action == IndexLifecycleAction.Synchronize)
            {
                return true;
            }
            if (action == IndexLifecycleAction.Rebuild && !await provider.RebuildAsync(index))
            {
                return false;
            }
            await _profiles.ResetAsync(index);
            await _profiles.UpdateAsync(index);
            return true;
        });
        if (result.Status == IndexProcessingStatus.Completed)
        {
            var context = new IndexProfileSynchronizedContext(profile) { IsIndexingCompleted = true };
            foreach (var handler in _services.GetServices<IIndexProfileHandler>())
            {
                await handler.SynchronizedAsync(context);
            }
        }
        return result;
    }
}
