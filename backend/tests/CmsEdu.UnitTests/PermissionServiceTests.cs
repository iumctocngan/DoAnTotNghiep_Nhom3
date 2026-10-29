using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CmsEdu.UnitTests;

public class PermissionServiceTests
{
    private static (AppDbContext Db, RoleManager<IdentityRole> RoleManager, IMemoryCache Cache) CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            new IRoleValidator<IdentityRole>[0],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new LoggerFactory().CreateLogger<RoleManager<IdentityRole>>());

        var cache = new MemoryCache(new MemoryCacheOptions());
        return (db, roleManager, cache);
    }

    [Fact]
    public async Task GetAllPermissions_ReturnsAllCatalogItems()
    {
        var (db, roleManager, cache) = CreateContext();
        var service = new PermissionService(db, roleManager, cache);

        var list = await service.GetAllPermissionsAsync();

        Assert.NotEmpty(list);
        Assert.Equal(Permissions.All.Count, list.Count);
        Assert.Contains(list, p => p.Code == Permissions.Students.View);
        Assert.Contains(list, p => p.Code == Permissions.Roles.Manage);
    }

    [Fact]
    public async Task AdminRole_AlwaysHasAllPermissions()
    {
        var (db, roleManager, cache) = CreateContext();
        var service = new PermissionService(db, roleManager, cache);

        var permissions = await service.GetPermissionsForRoleAsync(UserRole.Admin);

        Assert.Equal(Permissions.All.Count, permissions.Count);
        Assert.Contains(Permissions.Students.Delete, permissions);
    }

    [Fact]
    public async Task UpdateRolePermissions_Throws_WhenUpdatingAdmin()
    {
        var (db, roleManager, cache) = CreateContext();
        var service = new PermissionService(db, roleManager, cache);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateRolePermissionsAsync(UserRole.Admin, [Permissions.Students.View]));
    }

    [Fact]
    public async Task UpdateRolePermissions_Throws_WhenPermissionCodeInvalid()
    {
        var (db, roleManager, cache) = CreateContext();
        await roleManager.CreateAsync(new IdentityRole(UserRole.Teacher));
        var service = new PermissionService(db, roleManager, cache);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateRolePermissionsAsync(UserRole.Teacher, ["invalid.code"]));
    }

    [Fact]
    public async Task UpdateRolePermissions_SuccessfullyUpdatesAndCaches()
    {
        var (db, roleManager, cache) = CreateContext();
        await roleManager.CreateAsync(new IdentityRole(UserRole.Teacher));
        var service = new PermissionService(db, roleManager, cache);

        var targetPermissions = new List<string> { Permissions.Students.View, Permissions.Classes.View };
        await service.UpdateRolePermissionsAsync(UserRole.Teacher, targetPermissions);

        var permissions = await service.GetPermissionsForRoleAsync(UserRole.Teacher);
        Assert.Equal(2, permissions.Count);
        Assert.Contains(Permissions.Students.View, permissions);
        Assert.Contains(Permissions.Classes.View, permissions);
        Assert.DoesNotContain(Permissions.Students.Delete, permissions);
    }
}
