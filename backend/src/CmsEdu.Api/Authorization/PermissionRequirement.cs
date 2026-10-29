using Microsoft.AspNetCore.Authorization;

namespace CmsEdu.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
