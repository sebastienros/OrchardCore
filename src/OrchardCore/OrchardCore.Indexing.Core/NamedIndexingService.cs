using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace OrchardCore.Indexing.Core;

public abstract class NamedIndexingService
{
    protected readonly string Name;
    protected readonly ILogger Logger;

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IIndexingTaskManager _indexingTaskManager;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Gets the batch size for indexing operations. Can be overridden by derived classes to tune batch sizing.
    /// </summary>
    protected virtual int BatchSize => 100;

    protected NamedIndexingService(
        string name,
        IIndexProfileStore indexProfileStore,
        IIndexingTaskManager indexingTaskManager,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        _indexProfileStore = indexProfileStore;
        _indexingTaskManager = indexingTaskManager;
        _documentIndexHandlers = documentIndexHandlers;
        _serviceProvider = serviceProvider;
        Logger = logger;
    }

    public async Task ProcessRecordsForAllIndexesAsync()
    {
        var indexProfiles = await _indexProfileStore.GetByTypeAsync(Name);

        await ProcessRecordsAsync(indexProfiles);
    }

    public async Task ProcessRecordsAsync(IEnumerable<string> indexIds)
    {
        ArgumentNullException.ThrowIfNull(indexIds);

        if (!indexIds.Any())
        {
            return;
        }

        var indexProfiles = await _indexProfileStore.GetByTypeAsync(Name);

        await ProcessRecordsAsync(indexProfiles.Where(x => indexIds.Contains(x.Id)));
    }

    private async Task ProcessRecordsAsync(IEnumerable<IndexProfile> indexProfiles)
    {
        if (!indexProfiles.Any())
        {
            return;
        }

        var tracker = new Dictionary<string, IndexProfileEntryContext>();

        var documentIndexManagers = new Dictionary<string, IDocumentIndexManager>();
        var indexManagers = new Dictionary<string, IIndexManager>();

        var lastTaskId = long.MaxValue;

        var distributedLock = _serviceProvider.GetRequiredService<IDistributedLock>();
        var lockers = new List<ILocker>();

        try
        {
            // Find the lowest task id to process.
            foreach (var indexProfile in indexProfiles)
            {
                if (indexProfile.Type != Name)
                {
                    // Skip indexes that are not content indexes.
                    continue;
                }

                if (!documentIndexManagers.TryGetValue(indexProfile.ProviderName, out var documentIndexManager))
                {
                    documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

                    if (documentIndexManager is null)
                    {
                        Logger.LogWarning("Unable to find an implementation of {Implementation} for the provider '{ProviderName}'", nameof(IDocumentIndexManager), indexProfile.ProviderName);

                        continue;
                    }

                    documentIndexManagers.Add(indexProfile.ProviderName, documentIndexManager);
                }

                if (!indexManagers.TryGetValue(indexProfile.ProviderName, out var indexManager))
                {
                    indexManager = _serviceProvider.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

                    if (indexManager is null)
                    {
                        Logger.LogWarning("Unable to find an implementation of {Implementation} for the provider '{ProviderName}'", nameof(IIndexManager), indexProfile.ProviderName);

                        continue;
                    }

                    indexManagers.Add(indexProfile.ProviderName, indexManager);
                }

                if (!await indexManager.ExistsAsync(indexProfile.IndexFullName))
                {
                    Logger.LogWarning("The index '{IndexName}' does not exist for the provider '{ProviderName}'.", indexProfile.IndexName, indexProfile.ProviderName);

                    continue;
                }

                var taskId = await documentIndexManager.GetLastTaskIdAsync(indexProfile);
                lastTaskId = Math.Min(lastTaskId, taskId);
                tracker.Add(indexProfile.Id, new IndexProfileEntryContext(indexProfile, documentIndexManager, taskId));

                (var locker, var isLocked) = await distributedLock.TryAcquireLockAsync($"IndexingService-{indexProfile.Id}", TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(15));

                if (!isLocked)
                {
                    documentIndexManagers.Remove(indexProfile.ProviderName);
                    indexManagers.Remove(indexProfile.ProviderName);
                    tracker.Remove(indexProfile.Id);

                    Logger.LogWarning("The index {Name} is already being indexed. Skipping", indexProfile.Name);

                    continue;
                }

                lockers.Add(locker);
            }

            if (tracker.Count == 0)
            {
                return;
            }

            while (tracker.Count > 0)
            {
                List<RecordIndexingTask> currentBatch = null;

                try
                {
                    // Load the next batch of tasks.
                    currentBatch = (await _indexingTaskManager.GetIndexingTasksAsync(lastTaskId, BatchSize, Name)).ToList();

                    if (currentBatch.Count == 0)
                    {
                        break;
                    }

                    // Group all DocumentIndex by index to batch update them.
                    var updatedDocumentsByIndex = tracker.Values.ToDictionary(x => x.IndexProfile.Id, b => new List<DocumentIndex>());

                    var failedIndexes = new HashSet<string>();
                    await BeforeProcessingTasksAsync(currentBatch, tracker.Values);

                    foreach (var entry in tracker.Values)
                    {
                        foreach (var task in currentBatch)
                        {
                            if (task.Id <= entry.LastTaskId)
                            {
                                continue;
                            }

                            try
                            {
                                var buildIndexContext = await GetBuildDocumentIndexAsync(entry, task);

                                if (buildIndexContext is null)
                                {
                                    continue;
                                }

                                foreach (var handler in _documentIndexHandlers)
                                {
                                    await handler.BuildIndexAsync(buildIndexContext);
                                }

                                if (await ShouldTrackDocumentAsync(buildIndexContext, entry, task))
                                {
                                    updatedDocumentsByIndex[entry.IndexProfile.Id].Add(buildIndexContext.DocumentIndex);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Keep this index at its previous cursor so the failed batch can be retried.
                                Logger.LogError(ex, "Error processing indexing task {TaskId} for index {IndexName}. Stopping this index until the next run.", task.Id, entry.IndexProfile.Name);
                                failedIndexes.Add(entry.IndexProfile.Id);
                                break;
                            }
                        }
                    }

                    lastTaskId = currentBatch.Last().Id;

                    foreach (var indexEntry in updatedDocumentsByIndex)
                    {
                        if (failedIndexes.Contains(indexEntry.Key))
                        {
                            continue;
                        }

                        var trackerEntry = tracker[indexEntry.Key];

                        try
                        {
                            // AddOrUpdateDocumentsAsync is an upsert operation that handles both adding new documents
                            // and updating existing ones. Implementations should handle any necessary deletions internally.
                            if (indexEntry.Value.Count == 0 || await trackerEntry.DocumentIndexManager.AddOrUpdateDocumentsAsync(trackerEntry.IndexProfile, indexEntry.Value))
                            {
                                // Successfully filtered records also count as processed, without regressing ahead indexes.
                                if (lastTaskId > trackerEntry.LastTaskId)
                                {
                                    await trackerEntry.DocumentIndexManager.SetLastTaskIdAsync(trackerEntry.IndexProfile, lastTaskId);
                                }
                            }
                            else
                            {
                                failedIndexes.Add(indexEntry.Key);
                                Logger.LogWarning("The provider rejected documents for index {IndexName}. Stopping this index until the next run.", trackerEntry.IndexProfile.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            failedIndexes.Add(indexEntry.Key);
                            Logger.LogError(ex, "Error updating documents for index {IndexName}. Stopping this index until the next run.", trackerEntry.IndexProfile.Name);
                        }
                    }

                    foreach (var id in failedIndexes)
                    {
                        tracker.Remove(id);
                    }
                }
                catch (Exception ex)
                {
                    // Do not skip a failed batch and later advance a cursor past it.
                    Logger.LogError(ex, "Error processing a batch of indexing tasks. Stopping until the next run.");
                    break;
                }
            }
        }
        finally
        {
            foreach (var locker in lockers)
            {
                await locker.DisposeAsync();
            }
        }
    }

    protected abstract Task<BuildDocumentIndexContext> GetBuildDocumentIndexAsync(IndexProfileEntryContext entry, RecordIndexingTask task);

    protected virtual ValueTask<bool> ShouldTrackDocumentAsync(BuildDocumentIndexContext buildIndexContext, IndexProfileEntryContext entry, RecordIndexingTask task)
        => ValueTask.FromResult(true);

    protected virtual Task BeforeProcessingTasksAsync(IEnumerable<RecordIndexingTask> tasks, IEnumerable<IndexProfileEntryContext> contexts)
        => Task.CompletedTask;
}
