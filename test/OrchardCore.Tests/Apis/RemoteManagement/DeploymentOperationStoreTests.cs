using Microsoft.Extensions.Options;
using OrchardCore.Deployment.Operations;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentOperationStoreTests
{
    [Fact]
    public async Task Acceptance_DeduplicatesExactRequestAndRejectsChangedPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        try
        {
            var store = Store(root, "one");
            var token = TestContext.Current.CancellationToken;
            var operation = await store.CreateAsync("owner", "request", DeploymentOperationKind.Import, "artifact", token);
            var reopened = Store(root, "one");
            Assert.Equal(operation, await reopened.CreateAsync("owner", "request", DeploymentOperationKind.Import, "artifact", token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.CreateAsync("owner", "request", DeploymentOperationKind.Import, "different", token));
            Assert.Null(await reopened.FindAsync(operation.Id, "another", token));
            Assert.Null(await Store(root, "two").FindAsync(operation.Id, "owner", token));
            Assert.Null(await reopened.FindAsync("../operation.json", "owner", token));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_ExecutesOnceAndNeverReplaysInterruptedWork(bool complete)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        try
        {
            var token = TestContext.Current.CancellationToken;
            var store = Store(root, "one");
            var operation = await store.CreateAsync("owner", "request", DeploymentOperationKind.Import, "artifact", token);
            using (var claim = await store.ClaimAsync(operation.Id, token))
            {
                Assert.NotNull(claim);
                Assert.Equal(DeploymentOperationState.Running, (await store.FindAsync(operation.Id, "owner", token)).State);
                Assert.Null(await Store(root, "one").ClaimAsync(operation.Id, token));
                if (complete) { await claim.CompleteAsync(DeploymentOperationState.Succeeded, null, null, token); }
            }
            var reopened = Store(root, "one");
            Assert.Null(await reopened.ClaimAsync(operation.Id, token));
            var result = await reopened.FindAsync(operation.Id, "owner", token);
            Assert.Equal(complete ? DeploymentOperationState.Succeeded : DeploymentOperationState.Uncertain, result.State);
            Assert.Equal(complete ? null : "execution_interrupted", result.ErrorCode);
            Assert.Null(await reopened.ClaimAsync(operation.Id, token));
        }
        finally { Directory.Delete(root, true); }
    }

    private static DeploymentOperationStore Store(string root, string tenant)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => DateTime.UtcNow);
        return new DeploymentOperationStore(Options.Create(new ShellOptions { ShellsApplicationDataPath = root, ShellsContainerName = "Sites" }),
            new ShellSettings { Name = tenant }, clock.Object);
    }
}
