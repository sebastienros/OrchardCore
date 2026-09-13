using Microsoft.Extensions.FileProviders;

namespace OrchardCore.Deployment.Core.Services;

/// <summary>Owns validated private package files until import has finished.</summary>
public sealed class StagedDeploymentPackage : IDisposable
{
    private readonly string _folder;
    private readonly PhysicalFileProvider _provider;

    internal StagedDeploymentPackage(string folder)
    {
        _folder = folder;
        _provider = new PhysicalFileProvider(folder);
    }

    /// <summary>Gets the validated package files for the deployment manager.</summary>
    public IFileProvider FileProvider => _provider;

    /// <summary>Closes file watchers and removes staged files.</summary>
    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_folder)) { Directory.Delete(_folder, recursive: true); }
    }
}
