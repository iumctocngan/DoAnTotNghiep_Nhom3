using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CmsEdu.Infrastructure.Identity;

public class JwtAccessTokenGenerator(IOptions<JwtOptions> options)
    : IAccessTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult Generate(
        string userId,
        string employeeCode,
        string fullName,
        string email,
        string role)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, fullName),
            new Claim("employee_code", employeeCode),
            new Claim(ClaimTypes.Role, role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
