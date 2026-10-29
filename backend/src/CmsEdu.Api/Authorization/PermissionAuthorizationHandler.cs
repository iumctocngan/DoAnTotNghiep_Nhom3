using System.Security.Claims;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CmsEdu.Api.Authorization;

public class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // 1. Quản trị viên luôn có toàn quyền (SuperAdmin bypass)
        if (context.User.IsInRole(UserRole.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        // 2. Kiểm tra nếu trong Claims của User có claim Permission trực tiếp
        if (context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        // 3. Kiểm tra động theo vai trò hiện tại từ PermissionService (đã có bộ nhớ đệm MemoryCache)
        var userRole = context.User.FindFirstValue("role") ?? context.User.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrEmpty(userRole))
        {
            var rolePermissions = await permissionService.GetPermissionsForRoleAsync(userRole);
            if (rolePermissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
