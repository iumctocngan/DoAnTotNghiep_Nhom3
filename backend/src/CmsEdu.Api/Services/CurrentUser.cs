using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CmsEdu.Application.Common.Interfaces;

namespace CmsEdu.Api.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
