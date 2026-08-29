using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Remote Management",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "Provides secure remote management discovery for Orchard Core tenants.",
    Category = "Api"
)]

[assembly: Feature(
    Id = "OrchardCore.RemoteManagement",
    Name = "Remote Management",
    Description = "Enables the versioned remote management protocol used by the Orchard Core CLI.",
    Category = "Api",
    Dependencies =
    [
        "OrchardCore.OpenApi",
        "OrchardCore.OpenId.RemoteManagement",
    ]
)]
