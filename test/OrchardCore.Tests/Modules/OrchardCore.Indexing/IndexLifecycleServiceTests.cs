using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace OrchardCore.Tests.Modules.OrchardCore.Indexing;

public class IndexLifecycleServiceTests
{
    [Theory]
    [InlineData(IndexLifecycleAction.Synchronize)]
    [InlineData(IndexLifecycleAction.Reset)]
    [InlineData(IndexLifecycleAction.Rebuild)]
    public async Task Execute_Action_PreparesAndProcessesUnderOneLock(IndexLifecycleAction action)
    {
        var calls = new List<string>();
        var profile = new IndexProfile { Id = "index", Type = "Test", ProviderName = "Test", IndexFullName = "index" };
        var profiles = new Mock<IIndexProfileManager>();
        profiles.Setup(manager => manager.FindByIdAsync(profile.Id)).ReturnsAsync(profile);
        profiles.Setup(manager => manager.ResetAsync(profile)).Callback(() => calls.Add("reset"));
        profiles.Setup(manager => manager.UpdateAsync(profile, null)).Callback(() => calls.Add("update"));
        var store = new Mock<IIndexProfileStore>();
        var tasks = new Mock<IIndexingTaskManager>();
        tasks.Setup(manager => manager.GetIndexingTasksAsync(0, It.IsAny<int>(), "Test"))
            .Callback(() => calls.Add("tasks")).ReturnsAsync([]);
        var indexes = new Mock<IIndexManager>();
        indexes.Setup(manager => manager.RebuildAsync(profile)).Callback(() => calls.Add("rebuild")).ReturnsAsync(true);
        indexes.Setup(manager => manager.ExistsAsync("index")).Callback(() => calls.Add("exists")).ReturnsAsync(true);
        var documents = new Mock<IDocumentIndexManager>();
        documents.Setup(manager => manager.GetLastTaskIdAsync(profile)).Callback(() => calls.Add("cursor")).ReturnsAsync(0);
        var locker = new Mock<ILocker>();
        locker.Setup(value => value.DisposeAsync()).Callback(() => calls.Add("release"));
        var locking = new Mock<IDistributedLock>();
        locking.Setup(value => value.TryAcquireLockAsync("IndexingService-index", It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Callback(() => calls.Add("lock")).ReturnsAsync((locker.Object, true));
        using var services = new ServiceCollection().AddSingleton(locking.Object)
            .AddKeyedSingleton<IIndexManager>("Test", indexes.Object)
            .AddKeyedSingleton<IDocumentIndexManager>("Test", documents.Object)
            .AddKeyedSingleton<NamedIndexingService>("Test", (provider, _) => new Processor(store.Object, tasks.Object, provider))
            .BuildServiceProvider();
        var lifecycle = new IndexLifecycleService(profiles.Object, services);

        var result = await lifecycle.ExecuteAsync(profile.Id, action);

        Assert.Equal(IndexProcessingStatus.Completed, result.Status);
        var expected = new List<string> { "lock" };
        if (action == IndexLifecycleAction.Rebuild) { expected.Add("rebuild"); }
        if (action != IndexLifecycleAction.Synchronize) { expected.AddRange(["reset", "update"]); }
        expected.AddRange(["exists", "cursor", "tasks", "release"]);
        Assert.Equal(expected, calls);
        locking.Verify(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    private sealed class Processor : NamedIndexingService
    {
        public Processor(IIndexProfileStore store, IIndexingTaskManager tasks, IServiceProvider services)
            : base("Test", store, tasks, [], services, NullLogger.Instance)
        {
        }

        protected override Task<BuildDocumentIndexContext> GetBuildDocumentIndexAsync(IndexProfileEntryContext entry, RecordIndexingTask task)
            => throw new InvalidOperationException("The fixture queue must be empty.");
    }
}
