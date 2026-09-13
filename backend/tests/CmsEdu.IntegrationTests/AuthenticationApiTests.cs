using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Api.Controllers;
using CmsEdu.Application.Authentication;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.IntegrationTests;

public class AuthenticationApiTests {
    [KiemThuSqlServer]
    public async Task LoginReturnsTokenAndMeReturnsCurrentUser() {
        await using var app = new AuthenticationTestApp();
        await app.InitializeAsync();
        using var client = app.CreateTestClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthenticationTestApp.Email, AuthenticationTestApp.Password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(ReadRefreshToken(loginResponse)));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = (await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
        Assert.Equal(AuthenticationTestApp.EmployeeCode, me.EmployeeCode);
        Assert.Equal(AuthenticationTestApp.Email, me.Email);
        Assert.Equal(UserRole.Admin, me.Role);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthenticationTestApp.Email, "WrongPassword123"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        await app.SetUserInactiveAsync();
        var inactiveAccount = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthenticationTestApp.Email, AuthenticationTestApp.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveAccount.StatusCode);
    }

    [KiemThuSqlServer]
    public async Task RefreshRotatesTokenAndRejectsReuse() {
        await using var app = new AuthenticationTestApp();
        await app.InitializeAsync();
        using var client = app.CreateTestClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthenticationTestApp.Email, AuthenticationTestApp.Password));
        var firstToken = ReadRefreshToken(loginResponse);

        var refreshResponse = await PostWithRefreshTokenAsync(client, "/api/auth/refresh", firstToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var secondToken = ReadRefreshToken(refreshResponse);
        Assert.NotEqual(firstToken, secondToken);

        var reusedToken = await PostWithRefreshTokenAsync(client, "/api/auth/refresh", firstToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedToken.StatusCode);

        var revokedFamily = await PostWithRefreshTokenAsync(client, "/api/auth/refresh", secondToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamily.StatusCode);
    }

    [KiemThuSqlServer]
    public async Task LogoutAndLogoutAllRevokeRefreshTokens() {
        await using var app = new AuthenticationTestApp();
        await app.InitializeAsync();
        using var firstClient = app.CreateTestClient();
        using var secondClient = app.CreateTestClient();

        var firstLogin = await LoginAsync(firstClient);
        var firstToken = ReadRefreshToken(firstLogin.Response);
        var logout = await PostWithRefreshTokenAsync(firstClient, "/api/auth/logout", firstToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostWithRefreshTokenAsync(firstClient, "/api/auth/refresh", firstToken)).StatusCode);

        var secondLogin = await LoginAsync(firstClient);
        var thirdLogin = await LoginAsync(secondClient);
        firstClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", secondLogin.Body.AccessToken);

        var logoutAll = await firstClient.PostAsync("/api/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostWithRefreshTokenAsync(firstClient, "/api/auth/refresh", ReadRefreshToken(secondLogin.Response))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostWithRefreshTokenAsync(secondClient, "/api/auth/refresh", ReadRefreshToken(thirdLogin.Response))).StatusCode);
    }

    [KiemThuSqlServer]
    public async Task ChangePasswordRevokesSessionsAndRequiresNewPassword() {
        await using var app = new AuthenticationTestApp();
        await app.InitializeAsync();
        using var client = app.CreateTestClient();

        var login = await LoginAsync(client);
        var refreshToken = ReadRefreshToken(login.Response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Body.AccessToken);

        var changePassword = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(AuthenticationTestApp.Password, "NewPassword123"));
        Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostWithRefreshTokenAsync(client, "/api/auth/refresh", refreshToken)).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(AuthenticationTestApp.Email, AuthenticationTestApp.Password))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(AuthenticationTestApp.Email, "NewPassword123"))).StatusCode);
    }

    private static async Task<(HttpResponseMessage Response, LoginResponse Body)> LoginAsync(HttpClient client) {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthenticationTestApp.Email, AuthenticationTestApp.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (response, (await response.Content.ReadFromJsonAsync<LoginResponse>())!);
    }

    private static async Task<HttpResponseMessage> PostWithRefreshTokenAsync(
        HttpClient client,
        string path,
        string refreshToken) {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        return await client.SendAsync(request);
    }

    private static string ReadRefreshToken(HttpResponseMessage response) {
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("refreshToken=", StringComparison.Ordinal));
        return cookie.Split(';', 2)[0]["refreshToken=".Length..];
    }

    private sealed class AuthenticationTestApp : WebApplicationFactory<AuthController> {
        public const string Email = "admin@cms.edu.vn";
        public const string Password = "Password123";
        public const string EmployeeCode = "ADMIN001";
        private readonly string databaseName = $"MinhTests_{Guid.NewGuid():N}";
        private bool databaseCreated;

        public string ConnectionString { get; }

        public AuthenticationTestApp() {
            var connection = new SqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("CMSEDU_TEST_SQLSERVER"));
            connection.InitialCatalog = databaseName;
            ConnectionString = connection.ConnectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", "CmsEdu-Integration-Tests-Signing-Key-2026");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        }

        public HttpClient CreateTestClient() {
            return CreateClient(new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false
            });
        }

        public async Task InitializeAsync() {
            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.MigrateAsync();
            databaseCreated = true;

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole(UserRole.Admin))).Succeeded);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser {
                UserName = Email,
                Email = Email,
                EmailConfirmed = true,
                EmployeeCode = EmployeeCode,
                FullName = "Admin Test",
                EmploymentStatus = EmploymentStatus.Active
            };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, UserRole.Admin)).Succeeded);
        }

        public async Task SetUserInactiveAsync() {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await userManager.FindByEmailAsync(Email))!;
            user.EmploymentStatus = EmploymentStatus.Inactive;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        public override async ValueTask DisposeAsync() {
            if (!databaseCreated) {
                await base.DisposeAsync();
                return;
            }

            if (!databaseName.StartsWith("MinhTests_", StringComparison.Ordinal) ||
                new SqlConnectionStringBuilder(ConnectionString).InitialCatalog != databaseName) {
                throw new InvalidOperationException("Unexpected authentication test database name.");
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            await using var database = new AppDbContext(options);
            await database.Database.EnsureDeletedAsync();
            await base.DisposeAsync();
        }
    }
}
