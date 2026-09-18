using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using CmsEdu.Application.Staff;
using CmsEdu.Domain.Enums;

namespace CmsEdu.IntegrationTests;

public class CurriculumApiTests
{
    [KiemThuSqlServer]
    public async Task AdminCanManageCurriculumAndStaffCanReadIt()
    {
        await using var app = new AuthenticationApiTests.AuthenticationTestApp();
        await app.InitializeAsync();
        using var adminClient = app.CreateTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await adminClient.GetAsync("/api/courses")).StatusCode);

        var adminLogin = await adminClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthenticationApiTests.AuthenticationTestApp.Email,
                AuthenticationApiTests.AuthenticationTestApp.Password));
        var admin = (await adminLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var courseResponse = await adminClient.PostAsJsonAsync(
            "/api/courses",
            new CourseRequest("ENGLISH", "English", null));
        Assert.Equal(HttpStatusCode.Created, courseResponse.StatusCode);
        var course = (await courseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsJsonAsync(
            "/api/courses", new CourseRequest("ENGLISH", "Duplicate", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/courses/{course.Id}",
            new CourseRequest("ENGLISH", "English Updated", null))).StatusCode);

        var otherCourseResponse = await adminClient.PostAsJsonAsync(
            "/api/courses",
            new CourseRequest("OTHER", "Other Course", null));
        var otherCourse = (await otherCourseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PutAsJsonAsync(
            $"/api/courses/{otherCourse.Id}",
            new CourseRequest("ENGLISH", "Other Course", null))).StatusCode);

        var levelResponse = await adminClient.PostAsJsonAsync(
            "/api/levels",
            new LevelRequest(course.Id, "BEGINNER", "Beginner", 1));
        Assert.Equal(HttpStatusCode.Created, levelResponse.StatusCode);
        var level = (await levelResponse.Content.ReadFromJsonAsync<LevelResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsJsonAsync(
            "/api/levels", new LevelRequest(course.Id, "OTHER", "Duplicate order", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/levels/{level.Id}",
            new LevelRequest(course.Id, "BEGINNER", "Beginner Updated", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PutAsJsonAsync(
            $"/api/levels/{level.Id}",
            new LevelRequest(otherCourse.Id, "BEGINNER", "Beginner Updated", 1))).StatusCode);

        var otherLevelResponse = await adminClient.PostAsJsonAsync(
            "/api/levels",
            new LevelRequest(course.Id, "ADVANCED", "Advanced", 2));
        var otherLevel = (await otherLevelResponse.Content.ReadFromJsonAsync<LevelResponse>())!;

        var lessonResponse = await adminClient.PostAsJsonAsync(
            "/api/lessons",
            new LessonRequest(level.Id, "LESSON01", "Greetings", "Basic greetings", 1));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = (await lessonResponse.Content.ReadFromJsonAsync<LessonResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsJsonAsync(
            "/api/lessons", new LessonRequest(level.Id, "LESSON01", "Duplicate", null, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/lessons/{lesson.Id}",
            new LessonRequest(level.Id, "LESSON01", "Greetings Updated", "Basic greetings", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PutAsJsonAsync(
            $"/api/lessons/{lesson.Id}",
            new LessonRequest(otherLevel.Id, "LESSON01", "Greetings Updated", "Basic greetings", 1))).StatusCode);

        Assert.Single((await adminClient.GetFromJsonAsync<PagedResult<CourseResponse>>(
            "/api/courses?search=ENGLISH"))!.Items);
        Assert.Single((await adminClient.GetFromJsonAsync<PagedResult<LevelResponse>>(
            $"/api/levels?courseId={course.Id}&search=BEGINNER"))!.Items);
        Assert.Single((await adminClient.GetFromJsonAsync<PagedResult<LessonResponse>>(
            $"/api/lessons?levelId={level.Id}"))!.Items);

        var staffResponse = await adminClient.PostAsJsonAsync(
            "/api/staff",
            new CreateStaffRequest(
                "CARE001", "Customer Care", "care@test.local", null,
                UserRole.CustomerCare, "Password123"));
        Assert.Equal(HttpStatusCode.Created, staffResponse.StatusCode);

        using var staffClient = app.CreateTestClient();
        var staffLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("care@test.local", "Password123"));
        var staff = (await staffLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staff.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/courses")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PostAsJsonAsync(
            "/api/courses",
            new CourseRequest("BLOCKED", "Blocked", null))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/lessons/{lesson.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/levels/{level.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/courses/{course.Id}/deactivate", null)).StatusCode);

        Assert.False((await adminClient.GetFromJsonAsync<CourseResponse>(
            $"/api/courses/{course.Id}"))!.IsActive);
    }
}
