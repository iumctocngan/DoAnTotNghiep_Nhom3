namespace CmsEdu.Application.Permissions;

public record PermissionDto(string Code, string Name, string Group, string Description);

public record RolePermissionsMatrixDto(
    IReadOnlyList<PermissionDto> Permissions,
    IReadOnlyList<string> Roles,
    Dictionary<string, List<string>> RolePermissions);

public record UpdateRolePermissionsRequest(List<string> Permissions);
