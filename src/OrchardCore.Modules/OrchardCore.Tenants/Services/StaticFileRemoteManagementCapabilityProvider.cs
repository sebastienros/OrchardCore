using OrchardCore.RemoteManagement;

namespace OrchardCore.Tenants.Services;

internal sealed class StaticFileRemoteManagementCapabilityProvider : IRemoteManagementCapabilityProvider
{
    public const string CapabilityName = "static-files";

    public ValueTask<IEnumerable<RemoteManagementCapability>> GetCapabilitiesAsync() =>
        ValueTask.FromResult<IEnumerable<RemoteManagementCapability>>(
        [
            new RemoteManagementCapability
            {
                Id = CapabilityName,
                Version = "1.0",
                DisplayName = "Static Files",
            },
        ]);
}
