using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Permissions;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CmsEdu.Infrastructure.Identity;

public class PermissionService(
    AppDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    IMemoryCache memoryCache) : IPermissionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionDto> result = Permissions.All
            .Select(p => new PermissionDto(p.Code, p.Name, p.Group, p.Description))
            .ToList();

        return Task.FromResult(result);
    }

    public async Task<RolePermissionsMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default)
    {
        var allPermissions = await GetAllPermissionsAsync(cancellationToken);
        var matrix = new Dictionary<string, List<string>>();

        foreach (var role in UserRole.AllRoles)
        {
            matrix[role] = await GetPermissionsForRoleAsync(role, cancellationToken);
        }

        return new RolePermissionsMatrixDto(allPermissions, UserRole.AllRoles, matrix);
    }

    public async Task<List<string>> GetPermissionsForRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (roleName == UserRole.Admin)
        {
            return Permissions.All.Select(p => p.Code).ToList();
        }

        var cacheKey = GetCacheKey(roleName);
        if (memoryCache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
        {
            return cached;
        }

        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return new List<string>();
        }

        var permissions = await dbContext.RoleClaims
            .AsNoTracking()
            .Where(rc => rc.RoleId == role.Id && rc.ClaimType == Permissions.ClaimType && rc.ClaimValue != null)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync(cancellationToken);

        memoryCache.Set(cacheKey, permissions, CacheDuration);
        return permissions;
    }

    public async Task UpdateRolePermissionsAsync(
        string roleName,
        List<string> permissions,
        CancellationToken cancellationToken = default)
    {
        if (roleName == UserRole.Admin)
        {
            throw new ValidationException("Không thể chỉnh sửa quyền của Quản trị viên (Admin luôn có toàn quyền).");
        }

        if (!UserRole.AllRoles.Contains(roleName))
        {
            throw new ValidationException($"Vai trò '{roleName}' không hợp lệ.");
        }

        var validCodes = Permissions.All.Select(p => p.Code).ToHashSet();
        var invalid = permissions.Where(p => !validCodes.Contains(p)).ToList();
        if (invalid.Count > 0)
        {
            throw new ValidationException($"Các mã quyền không hợp lệ: {string.Join(", ", invalid)}");
        }

        var role = await roleManager.FindByNameAsync(roleName)
            ?? throw new NotFoundException($"Không tìm thấy vai trò '{roleName}'.");

        var distinctPermissions = permissions.Distinct().ToList();

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var existingClaims = await dbContext.RoleClaims
                .Where(rc => rc.RoleId == role.Id && rc.ClaimType == Permissions.ClaimType)
                .ToListAsync(cancellationToken);

            dbContext.RoleClaims.RemoveRange(existingClaims);

            foreach (var perm in distinctPermissions)
            {
                dbContext.RoleClaims.Add(new IdentityRoleClaim<string>
                {
                    RoleId = role.Id,
                    ClaimType = Permissions.ClaimType,
                    ClaimValue = perm
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            var existingClaims = await dbContext.RoleClaims
                .Where(rc => rc.RoleId == role.Id && rc.ClaimType == Permissions.ClaimType)
                .ToListAsync(cancellationToken);

            dbContext.RoleClaims.RemoveRange(existingClaims);

            foreach (var perm in distinctPermissions)
            {
                dbContext.RoleClaims.Add(new IdentityRoleClaim<string>
                {
                    RoleId = role.Id,
                    ClaimType = Permissions.ClaimType,
                    ClaimValue = perm
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        InvalidateCache(roleName);
    }

    public void InvalidateCache(string? roleName = null)
    {
        if (roleName is not null)
        {
            memoryCache.Remove(GetCacheKey(roleName));
        }
        else
        {
            foreach (var role in UserRole.AllRoles)
            {
                memoryCache.Remove(GetCacheKey(role));
            }
        }
    }

    private static string GetCacheKey(string roleName) => $"RolePermissions_{roleName}";
}
