using CmsEdu.Application.Permissions;

namespace CmsEdu.Application.Common.Interfaces;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
    Task<RolePermissionsMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetPermissionsForRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(string roleName, List<string> permissions, CancellationToken cancellationToken = default);
    void InvalidateCache(string? roleName = null);
}
