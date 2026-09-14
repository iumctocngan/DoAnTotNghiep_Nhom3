using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Staff;
using CmsEdu.Domain.Enums;

namespace CmsEdu.IntegrationTests;

public class StaffApiTests
{
    [KiemThuSqlServer]
    public async Task AdminCanManageStaffAndOtherRolesAreForbidden()
    {
        await using var app = new AuthenticationApiTests.AuthenticationTestApp();
        await app.InitializeAsync();
        using var adminClient = app.CreateTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await adminClient.GetAsync("/api/staff")).StatusCode);

        var adminLogin = await adminClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthenticationApiTests.AuthenticationTestApp.Email,
                AuthenticationApiTests.AuthenticationTestApp.Password));
        var admin = (await adminLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var create = await adminClient.PostAsJsonAsync(
            "/api/staff",
            new CreateStaffRequest(
                "TEACHER001", "Teacher Test", "teacher@test.local", null,
                UserRole.Teacher, "Password123"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var teacher = (await create.Content.ReadFromJsonAsync<StaffResponse>())!;

        using var teacherClient = app.CreateTestClient();
        var teacherLogin = await teacherClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("teacher@test.local", "Password123"));
        var teacherSession = (await teacherLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        teacherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", teacherSession.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.GetAsync("/api/staff")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/staff/{teacher.Id}",
            new UpdateStaffRequest("Teacher Updated", "teacher.updated@test.local", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/staff/{teacher.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync(
            $"/api/staff/{teacher.Id}/role",
            new ChangeStaffRoleRequest(UserRole.CustomerCare))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(
            $"/api/staff/{teacher.Id}/reset-password",
            new ResetStaffPasswordRequest("NewPassword123"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/staff/{teacher.Id}/activate", null)).StatusCode);

        var updatedLogin = await adminClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("teacher.updated@test.local", "NewPassword123"));
        var updated = (await updatedLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(HttpStatusCode.OK, updatedLogin.StatusCode);
        Assert.Equal(UserRole.CustomerCare, updated.Role);
    }
}
