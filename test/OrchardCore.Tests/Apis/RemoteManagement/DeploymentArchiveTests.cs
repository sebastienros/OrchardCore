using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Core.Services;
using OrchardCore.Deployment.Services;
using OrchardCore.FileStorage;
using OrchardCore.Recipes.Models;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentArchiveTests
{
    [Fact]
    public async Task ConcurrentExports_PreserveRecipeAndFiles_AndCleanUpOnDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var temporary = new Mock<ITempDirectoryProvider>();
            temporary.Setup(provider => provider.GetRootDirectory()).Returns(root);
            var source = new Mock<IDeploymentSource>();
            source.Setup(value => value.ProcessDeploymentStepAsync(It.IsAny<DeploymentStep>(), It.IsAny<DeploymentPlanResult>()))
                .Returns(async (DeploymentStep _, DeploymentPlanResult result) =>
                    await result.FileBuilder.SetFileAsync("nested/sample.txt", Encoding.UTF8.GetBytes("payload")));
            var manager = new DeploymentManager([source.Object], [], []);
            var service = new DeploymentArchiveService(manager, temporary.Object);
            var plan = new DeploymentPlan { Name = "Same name", DeploymentSteps = [new UnknownDeploymentStep()] };
            var first = await service.CreateAsync(plan, new RecipeDescriptor { Name = "First", Tags = ["test"] });
            var second = await service.CreateAsync(plan, new RecipeDescriptor { Name = "Second" });
            await using (first)
            await using (second)
            {
                Assert.Empty(Directory.GetDirectories(root));
                using var zip = new ZipArchive(first, ZipArchiveMode.Read, leaveOpen: true);
                using var json = await JsonDocument.ParseAsync(zip.GetEntry("Recipe.json").Open(), cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal("First", json.RootElement.GetProperty("name").GetString());
                Assert.Equal("test", json.RootElement.GetProperty("tags")[0].GetString());
                using var reader = new StreamReader(zip.GetEntry("nested/sample.txt").Open());
                Assert.Equal("payload", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
                using var other = new ZipArchive(second, ZipArchiveMode.Read, leaveOpen: true);
                using var otherJson = await JsonDocument.ParseAsync(other.GetEntry("Recipe.json").Open(), cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal("Second", otherJson.RootElement.GetProperty("name").GetString());
            }
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailedExport_RemovesStagedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var temporary = new Mock<ITempDirectoryProvider>();
            temporary.Setup(provider => provider.GetRootDirectory()).Returns(root);
            var manager = new Mock<IDeploymentManager>();
            manager.Setup(value => value.ExecuteDeploymentPlanAsync(It.IsAny<DeploymentPlan>(), It.IsAny<DeploymentPlanResult>()))
                .Returns(async (DeploymentPlan _, DeploymentPlanResult result) =>
                {
                    await result.FileBuilder.SetFileAsync("partial.txt", Encoding.UTF8.GetBytes("partial"));
                    throw new InvalidOperationException("Source failed");
                });
            var service = new DeploymentArchiveService(manager.Object, temporary.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new DeploymentPlan(), new RecipeDescriptor()));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
