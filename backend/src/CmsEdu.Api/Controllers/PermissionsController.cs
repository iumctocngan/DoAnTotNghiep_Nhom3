using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Permissions;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetAll(CancellationToken ct) =>
        Ok(await permissionService.GetAllPermissionsAsync(ct));

    [HttpGet("matrix")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<RolePermissionsMatrixDto>> GetMatrix(CancellationToken ct) =>
        Ok(await permissionService.GetMatrixAsync(ct));

    [HttpGet("roles/{roleName}")]
    public async Task<ActionResult<List<string>>> GetRolePermissions(string roleName, CancellationToken ct) =>
        Ok(await permissionService.GetPermissionsForRoleAsync(roleName, ct));

    [HttpPut("roles/{roleName}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> UpdateRolePermissions(
        string roleName,
        UpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        await permissionService.UpdateRolePermissionsAsync(roleName, request.Permissions, ct);
        return NoContent();
    }
}
