using Microsoft.AspNetCore.Authorization;
using OrchardCore.Roles.Endpoints.Management;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Roles;

public class RoleManagementEndpointsTests
{
    [Fact]
    public async Task ToResponseAsync_AdminRoleMarksAllPermissionsEffective()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        var roleService = new Mock<IRoleService>();
        roleService.Setup(service => service.IsSystemRoleAsync(OrchardCoreConstants.Roles.Administrator)).ReturnsAsync(true);
        roleService.Setup(service => service.IsAdminRoleAsync(OrchardCoreConstants.Roles.Administrator)).ReturnsAsync(true);

        var permissionCatalog = new PermissionCatalog(
            [
                new Permission("ManageUsers") { Description = "Manage users", Category = "Users" },
                new Permission("ManageRoles") { Description = "Manage roles", Category = "Roles" },
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ManageUsers"] = "OrchardCore.Users",
                ["ManageRoles"] = "OrchardCore.Roles",
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ManageUsers"] = "Users",
                ["ManageRoles"] = "Roles",
            });

        var response = await RoleManagementEndpoints.ToResponseAsync(new Role
        {
            RoleName = OrchardCoreConstants.Roles.Administrator,
            RoleClaims = [RoleClaim.Create("ManageUsers")],
        }, authorizationService.Object, roleService.Object, permissionCatalog);

        Assert.True(response.IsSystemRole);
        Assert.True(response.IsAdminRole);
        Assert.Equal(2, response.PermissionCount);
        Assert.All(response.Permissions, permission => Assert.True(permission.IsEffective));
        Assert.True(response.Permissions.Single(permission => permission.Name == "ManageUsers").IsAssigned);
        Assert.False(response.Permissions.Single(permission => permission.Name == "ManageRoles").IsAssigned);
    }
}
