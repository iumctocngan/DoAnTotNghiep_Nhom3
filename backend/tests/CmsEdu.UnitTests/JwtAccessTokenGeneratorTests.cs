using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CmsEdu.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CmsEdu.UnitTests;

public class JwtAccessTokenGeneratorTests {
    private const string SigningKey = "CmsEdu-Unit-Tests-Signing-Key-2026";

    [Fact]
    public void GenerateCreatesValidTokenWithExpectedClaims() {
        var generator = CreateGenerator(SigningKey);

        var result = generator.Generate(
            "user-1",
            "NV001",
            "Nguyen Van A",
            "a@cms.edu.vn",
            "Teacher");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal("CmsEdu.Api", token.Issuer);
        Assert.Contains("CmsEdu.Web", token.Audiences);
        Assert.Equal("user-1", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("NV001", token.Claims.Single(claim => claim.Type == "employee_code").Value);
        Assert.Equal("Nguyen Van A", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("a@cms.edu.vn", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Teacher", token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.Equal(result.ExpiresAtUtc, token.ValidTo);
        Assert.InRange(result.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void TokenSignedWithAnotherKeyIsRejected() {
        var result = CreateGenerator(SigningKey).Generate(
            "user-1",
            "NV001",
            "Nguyen Van A",
            "a@cms.edu.vn",
            "Teacher");

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validation = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = "CmsEdu.Api",
            ValidateAudience = true,
            ValidAudience = "CmsEdu.Web",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("Another-Unit-Test-Signing-Key-2026")),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(result.Token, validation, out _));
    }

    private static JwtAccessTokenGenerator CreateGenerator(string signingKey) {
        return new JwtAccessTokenGenerator(Options.Create(new JwtOptions {
            Issuer = "CmsEdu.Api",
            Audience = "CmsEdu.Web",
            SigningKey = signingKey,
            AccessTokenMinutes = 15
        }));
    }
}
