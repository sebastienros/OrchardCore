using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking.Distributed;

namespace OrchardCore.Indexing.Core;

/// <summary>Executes index lifecycle work through the shared worker and its per-index lock.</summary>
public interface IIndexLifecycleService
{
    /// <summary>Executes an action and returns its processing outcome. Legacy extension handlers may schedule additional work; such execution returns an unverified outcome.</summary>
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
            return await ExecuteLegacyAsync(profile, action);
        }
        var result = await processor.ProcessIndexWithPreparationAsync(profile,
            (index, provider) => PrepareAsync(index, provider, action));
        if (result.Status == IndexProcessingStatus.Completed)
        {
            await NotifyAsync(profile, indexingCompleted: true);
        }
        return result;
    }

    private async Task<bool> PrepareAsync(IndexProfile profile, IIndexManager provider, IndexLifecycleAction action)
    {
        if (action == IndexLifecycleAction.Synchronize) { return true; }
        if (action == IndexLifecycleAction.Rebuild && !await provider.RebuildAsync(profile)) { return false; }
        await _profiles.ResetAsync(profile);
        await _profiles.UpdateAsync(profile);
        return true;
    }

    private async Task<IndexProcessingResult> ExecuteLegacyAsync(IndexProfile profile, IndexLifecycleAction action)
    {
        if (action != IndexLifecycleAction.Synchronize)
        {
            var provider = _services.GetKeyedService<IIndexManager>(profile.ProviderName);
            if (action == IndexLifecycleAction.Rebuild && provider is null)
            {
                return new IndexProcessingResult(profile.Id, IndexProcessingStatus.ProviderUnavailable);
            }
            var locking = _services.GetRequiredService<IDistributedLock>();
            var (locker, locked) = await locking.TryAcquireLockAsync("IndexingService-" + profile.Id,
                TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(15));
            if (!locked) { return new IndexProcessingResult(profile.Id, IndexProcessingStatus.Busy); }
            await using (locker)
            {
                if (!await PrepareAsync(profile, provider, action))
                {
                    return new IndexProcessingResult(profile.Id, IndexProcessingStatus.ProviderRejected);
                }
            }
        }

        // Legacy handlers may acquire their own lock or schedule another job. Their
        // void result cannot establish that indexing completed, even after they return.
        await NotifyAsync(profile, indexingCompleted: false);
        return new IndexProcessingResult(profile.Id, IndexProcessingStatus.Unverified);
    }

    private async Task NotifyAsync(IndexProfile profile, bool indexingCompleted)
    {
        var context = new IndexProfileSynchronizedContext(profile) { IsIndexingCompleted = indexingCompleted };
        foreach (var handler in _services.GetServices<IIndexProfileHandler>())
        {
            await handler.SynchronizedAsync(context);
        }
    }
}
