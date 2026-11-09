using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using CmsEdu.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace CmsEdu.IntegrationTests;

public class CskhChatAuthorizationTests
{
    [Fact]
    public async Task Module3Endpoints_RequireAdminOrCustomerCareRole()
    {
        await using var application = new TestApplication();
        using var teacher = application.CreateClient();
        teacher.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader("Teacher");

        var requests = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, "/api/ai/dashboard/retention-queue"),
            (HttpMethod.Get, "/api/ai/cskh/student-context/1"),
            (HttpMethod.Post, "/api/ai/cskh-chat/sessions"),
            (HttpMethod.Post, "/api/ai/cskh-chat/sessions/test-session/messages"),
            (HttpMethod.Get, "/api/ai/cskh-chat/sessions/test-session/history"),
            (HttpMethod.Post, "/api/cskh/interactions")
        };

        foreach (var (method, path) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await teacher.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Module3Endpoints_RejectAnonymousRequests()
    {
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/ai/dashboard/retention-queue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static AuthenticationHeaderValue CreateAuthorizationHeader(string role)
    {
        var token = new JwtSecurityToken(
            "CmsEdu.Api",
            "CmsEdu.Web",
            [
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim("role", role)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplication.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class TestApplication : WebApplicationFactory<CskhChatController>
    {
        public const string SigningKey = "CmsEdu-Module3-Authorization-Tests-Signing-Key";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", SigningKey);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;");
        }
    }
}
