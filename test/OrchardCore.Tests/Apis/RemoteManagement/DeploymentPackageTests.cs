using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment.Core.Services;
using OrchardCore.FileStorage;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentPackageTests
{
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("folder\\escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("folder/./escape.txt")]
    [InlineData("folder//escape.txt")]
    public async Task UnsafeArchivePath_IsRejectedAndStagingRemoved(string path)
    {
        await WithService(async (service, root) =>
        {
            using var input = Zip(("Recipe.json", "{\"steps\":[]}"), (path, "payload"));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.StageAsync(input, "package.zip", TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFileSystemEntries(root));
            Assert.True(input.CanRead);
        });
    }

    [Fact]
    public async Task ValidZip_PreservesFilesAndOwnsCleanup()
    {
        await WithService(async (service, root) =>
        {
            using var input = Zip(("Recipe.json", "{\"steps\":[{\"name\":\"settings\"}]}"), ("nested/file.txt", "payload"));
            using (var package = await service.StageAsync(input, "package.ZIP", TestContext.Current.CancellationToken))
            {
                using var reader = new StreamReader(package.FileProvider.GetFileInfo("nested/file.txt").CreateReadStream());
                Assert.Equal("payload", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
            }
            Assert.Empty(Directory.GetFileSystemEntries(root));
            Assert.True(input.CanRead);
        });
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing-recipe")]
    [InlineData("invalid-recipe")]
    [InlineData("expanded-limit")]
    [InlineData("upload-limit")]
    [InlineData("entry-limit")]
    public async Task InvalidPackageOrLimit_IsRejectedWithoutResidualFiles(string scenario)
    {
        var options = new DeploymentPackageOptions();
        if (scenario == "expanded-limit") { options.MaxExpandedBytes = 5; }
        if (scenario == "upload-limit") { options.MaxUploadBytes = 5; }
        if (scenario == "entry-limit") { options.MaxEntries = 1; }
        await WithService(async (service, root) =>
        {
            using var input = scenario switch
            {
                "duplicate" => Zip(("Recipe.json", "{\"steps\":[]}"), ("recipe.json", "{}")),
                "missing-recipe" => Zip(("other.json", "{}")),
                "invalid-recipe" => Zip(("Recipe.json", "{\"steps\":[{}]}")),
                _ => Zip(("Recipe.json", "{\"steps\":[]}"), ("file.txt", "payload")),
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => service.StageAsync(input, "package.zip", TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }, options);
    }

    private static MemoryStream Zip(params (string Path, string Value)[] files)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, value) in files)
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(value));
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static async Task WithService(Func<DeploymentPackageService, string, Task> test, DeploymentPackageOptions options = null)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var temporary = new Mock<ITempDirectoryProvider>();
            temporary.Setup(provider => provider.CreateTempSubdirectory()).Returns(() =>
            {
                var folder = Path.Combine(root, Guid.NewGuid().ToString("n"));
                Directory.CreateDirectory(folder);
                return folder;
            });
            await test(new DeploymentPackageService(temporary.Object, Options.Create(options ?? new())), root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
