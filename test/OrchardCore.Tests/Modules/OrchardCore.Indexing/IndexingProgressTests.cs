using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace OrchardCore.Tests.Modules.OrchardCore.Indexing;

public class IndexingProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessRecords_FailedBatch_DoesNotAdvancePastFailure(bool handlerFailure)
    {
        var profile = new IndexProfile { Id = "index", Name = "Index", ProviderName = "Test", Type = "Test", IndexFullName = "index" };
        var profiles = new Mock<IIndexProfileStore>();
        profiles.Setup(store => store.GetByTypeAsync("Test")).ReturnsAsync([profile]);
        var tasks = new Mock<IIndexingTaskManager>();
        tasks.Setup(manager => manager.GetIndexingTasksAsync(It.IsAny<long>(), It.IsAny<int>(), "Test"))
            .ReturnsAsync((long after, int count, string category) => after < 2
                ? new[] { new RecordIndexingTask { Id = after + 1, RecordId = "record" + after, Category = category } }
                : []);
        var documents = new Mock<IDocumentIndexManager>();
        if (handlerFailure)
        {
            documents.Setup(manager => manager.AddOrUpdateDocumentsAsync(profile, It.IsAny<IEnumerable<DocumentIndex>>())).ReturnsAsync(true);
        }
        else
        {
            documents.SetupSequence(manager => manager.AddOrUpdateDocumentsAsync(profile, It.IsAny<IEnumerable<DocumentIndex>>()))
                .ReturnsAsync(false).ReturnsAsync(true);
        }
        var handler = new Mock<IDocumentIndexHandler>();
        handler.SetupSequence(value => value.BuildIndexAsync(It.IsAny<BuildDocumentIndexContext>()))
            .Returns(handlerFailure ? Task.FromException(new InvalidOperationException("Document build failed")) : Task.CompletedTask)
            .Returns(Task.CompletedTask);
        using var services = CreateServices(documents.Object);
        var indexing = new TestIndexingService(profiles.Object, tasks.Object, [handler.Object], services);

        await indexing.ProcessRecordsAsync([profile.Id]);

        documents.Verify(manager => manager.SetLastTaskIdAsync(profile, It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRecords_FilteredBatch_AdvancesCursorWithoutProviderWrite()
    {
        var profile = new IndexProfile { Id = "index", Name = "Index", ProviderName = "Test", Type = "Test", IndexFullName = "index" };
        var profiles = new Mock<IIndexProfileStore>();
        profiles.Setup(store => store.GetByTypeAsync("Test")).ReturnsAsync([profile]);
        var tasks = new Mock<IIndexingTaskManager>();
        tasks.Setup(manager => manager.GetIndexingTasksAsync(It.IsAny<long>(), It.IsAny<int>(), "Test"))
            .ReturnsAsync((long after, int count, string category) => after == 0
                ? new[] { new RecordIndexingTask { Id = 1, RecordId = "filtered", Category = category } }
                : []);
        var documents = new Mock<IDocumentIndexManager>();
        using var services = CreateServices(documents.Object);
        var indexing = new TestIndexingService(profiles.Object, tasks.Object, [], services) { FilterDocuments = true };

        await indexing.ProcessRecordsAsync([profile.Id]);

        documents.Verify(manager => manager.SetLastTaskIdAsync(profile, 1), Times.Once);
        documents.Verify(manager => manager.AddOrUpdateDocumentsAsync(profile, It.IsAny<IEnumerable<DocumentIndex>>()), Times.Never);
    }

    private static ServiceProvider CreateServices(IDocumentIndexManager documents)
    {
        var indexes = new Mock<IIndexManager>();
        indexes.Setup(manager => manager.ExistsAsync("index")).ReturnsAsync(true);
        var locking = new Mock<IDistributedLock>();
        locking.Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));
        return new ServiceCollection().AddSingleton(locking.Object)
            .AddKeyedSingleton<IDocumentIndexManager>("Test", documents)
            .AddKeyedSingleton<IIndexManager>("Test", indexes.Object).BuildServiceProvider();
    }

    private sealed class TestIndexingService : NamedIndexingService
    {
        public bool FilterDocuments { get; init; }

        public TestIndexingService(IIndexProfileStore profiles, IIndexingTaskManager tasks,
            IEnumerable<IDocumentIndexHandler> handlers, IServiceProvider services)
            : base("Test", profiles, tasks, handlers, services, NullLogger.Instance)
        {
        }

        protected override Task<BuildDocumentIndexContext> GetBuildDocumentIndexAsync(IndexProfileEntryContext entry, RecordIndexingTask task)
            => Task.FromResult(FilterDocuments ? null : new BuildDocumentIndexContext(new DocumentIndex(task.RecordId), new object(), [], null));
    }
}
