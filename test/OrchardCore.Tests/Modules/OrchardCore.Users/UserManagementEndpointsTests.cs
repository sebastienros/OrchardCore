using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Endpoints.Management;
using OrchardCore.Users.Models;

namespace OrchardCore.Tests.Modules.OrchardCore.Users;

public class UserManagementEndpointsTests
{
    [Fact]
    public void ToResponse_SortsRolesAndOmitsSensitiveFields()
    {
        var response = UserManagementEndpoints.ToResponse(new User
        {
            UserId = "user-1",
            UserName = "alice",
            Email = "alice@example.com",
            PhoneNumber = "555-0100",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            ResetToken = "reset-token",
            EmailConfirmed = true,
            IsEnabled = true,
            IsLockoutEnabled = true,
            AccessFailedCount = 2,
            RoleNames = ["Editor", "Author"],
        });

        var json = JsonSerializer.Serialize(response);

        Assert.Equal(["Author", "Editor"], response.RoleNames);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_SoleEnabledAdministrator_PreservesAdministratorRole()
    {
        var user = new User
        {
            UserId = "admin",
            UserName = "admin",
            IsEnabled = true,
            RoleNames = [OrchardCoreConstants.Roles.Administrator],
        };
        var roles = new List<string>(user.RoleNames);
        var roleStore = new Mock<IUserRoleStore<IUser>>();
        roleStore
            .Setup(store => store.GetRolesAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => roles);
        roleStore
            .Setup(store => store.RemoveFromRoleAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IUser, string, CancellationToken>((_, role, _) => roles.RemoveAll(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase)))
            .Returns(Task.CompletedTask);

        var userStore = Mock.Of<IUserStore<IUser>>();
        var userManager = new Mock<UserManager<IUser>>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<IUser>(),
            Array.Empty<IUserValidator<IUser>>(),
            Array.Empty<IPasswordValidator<IUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<IUser>>.Instance);
        userManager
            .Setup(manager => manager.GetUsersInRoleAsync(OrchardCoreConstants.Roles.Administrator))
            .ReturnsAsync([user]);
        userManager
            .Setup(manager => manager.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var services = new ServiceCollection()
            .AddSingleton(roleStore.Object)
            .BuildServiceProvider();

        await UserManagementEndpoints.UpdateUserRolesAsync(services, userManager.Object, user, []);

        Assert.Contains(OrchardCoreConstants.Roles.Administrator, user.RoleNames);
        roleStore.Verify(
            store => store.RemoveFromRoleAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
