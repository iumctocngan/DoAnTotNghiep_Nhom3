using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Staff;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.IntegrationTests;

public class TeacherApiTests
{
    [KiemThuSqlServer]
    public async Task AdminCanReadTeacherAssignmentsAndTeacherCanOnlyReadTheirOwn()
    {
        await using var app = new AuthenticationApiTests.AuthenticationTestApp();
        await app.InitializeAsync();
        using var adminClient = app.CreateTestClient();

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
        var teacher = (await create.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        await SeedAssignmentsAsync(app, teacher.Id);

        var adminClasses = await adminClient.GetFromJsonAsync<PagedResult<TeacherClassResponse>>(
            $"/api/teachers/{teacher.Id}/classes");
        Assert.Single(adminClasses!.Items);

        using var teacherClient = app.CreateTestClient();
        var teacherLogin = await teacherClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("teacher@test.local", "Password123"));
        var teacherSession = (await teacherLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        teacherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", teacherSession.AccessToken);

        var schedule = await teacherClient.GetFromJsonAsync<PagedResult<TeacherScheduleResponse>>(
            $"/api/teachers/{teacher.Id}/schedule");
        Assert.Single(schedule!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.GetAsync(
            "/api/teachers/another-teacher/classes")).StatusCode);
    }

    private static async Task SeedAssignmentsAsync(
        AuthenticationApiTests.AuthenticationTestApp app,
        string teacherId)
    {
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Code = "COURSE001", Name = "Course 1" };
        var level = new Level { Course = course, Code = "LEVEL001", Name = "Level 1", SortOrder = 1 };
        var assignedClass = new Class
        {
            ClassCode = "CLASS001",
            Name = "Class 1",
            Level = level,
            MainTeacherUserId = teacherId,
            Capacity = 20,
            StartDate = new DateOnly(2026, 9, 1),
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = ClassStatus.Active
        };
        database.Sessions.Add(new Session
        {
            Class = assignedClass,
            SessionDate = new DateOnly(2026, 9, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SessionStatus.Scheduled
        });
        await database.SaveChangesAsync();
    }
}
