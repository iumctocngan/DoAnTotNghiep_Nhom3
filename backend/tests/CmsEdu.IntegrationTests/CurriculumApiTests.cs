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
    public async Task NumberedLessonsAreSortedAutomatically()
    {
        await using var app = new AuthenticationApiTests.AuthenticationTestApp();
        await app.InitializeAsync();
        using var client = app.CreateTestClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(AuthenticationApiTests.AuthenticationTestApp.Email, AuthenticationApiTests.AuthenticationTestApp.Password));
        var credentials = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        using var course = await client.PostAsJsonAsync("/api/courses", new CourseRequest("AUTO", "Toán", null));
        var courseId = (await course.Content.ReadFromJsonAsync<CourseResponse>())!.Id;
        using var level = await client.PostAsJsonAsync("/api/levels", new LevelRequest(courseId, "AUTO", "Cấp độ"));
        var levelId = (await level.Content.ReadFromJsonAsync<LevelResponse>())!.Id;
        foreach (var title in new[] { "B12 – Tuần 1", "Ôn tập", "B11 – Tuần 10", "B21 – Tuần 1", "B11 – Tuần 2" })
        {
            using var create = await client.PostAsJsonAsync("/api/lessons", new LessonRequest(levelId, "AUTO", title, null));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var result = await client.GetFromJsonAsync<PagedResult<LessonResponse>>($"/api/lessons?levelId={levelId}");
        Assert.Equal(new[] { "B11 – Tuần 2", "Ôn tập", "B11 – Tuần 10", "B12 – Tuần 1", "B21 – Tuần 1" },
            result!.Items.Select(x => x.Name));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.Items.Select(x => x.SortOrder));
        foreach (var lesson in result.Items)
        {
            var saved = await client.GetFromJsonAsync<LessonResponse>($"/api/lessons/{lesson.Id}");
            Assert.Equal(lesson.SortOrder, saved!.SortOrder);
        }
    }

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
        Assert.Equal("KH001", course.Code);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/courses/{course.Id}",
            new CourseRequest("ENGLISH", "English Updated", null))).StatusCode);

        var otherCourseResponse = await adminClient.PostAsJsonAsync(
            "/api/courses",
            new CourseRequest("OTHER", "Other Course", null));
        var otherCourse = (await otherCourseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/courses/{otherCourse.Id}",
            new CourseRequest("ENGLISH", "Other Course", null))).StatusCode);

        var levelResponse = await adminClient.PostAsJsonAsync(
            "/api/levels",
            new LevelRequest(course.Id, "BEGINNER", "Beginner"));
        Assert.Equal(HttpStatusCode.Created, levelResponse.StatusCode);
        var level = (await levelResponse.Content.ReadFromJsonAsync<LevelResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/levels/{level.Id}",
            new LevelRequest(course.Id, "BEGINNER", "Beginner Updated"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PutAsJsonAsync(
            $"/api/levels/{level.Id}",
            new LevelRequest(otherCourse.Id, "BEGINNER", "Beginner Updated"))).StatusCode);

        Assert.StartsWith("CD", level.Code);
        var otherLevelResponse = await adminClient.PostAsJsonAsync(
            "/api/levels",
            new LevelRequest(course.Id, "ADVANCED", "Advanced"));
        var otherLevel = (await otherLevelResponse.Content.ReadFromJsonAsync<LevelResponse>())!;

        var lessonResponse = await adminClient.PostAsJsonAsync(
            "/api/lessons",
            new LessonRequest(level.Id, "LESSON01", "Greetings", "Basic greetings"));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = (await lessonResponse.Content.ReadFromJsonAsync<LessonResponse>())!;
        Assert.Equal("BH001", lesson.Code);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/lessons/{lesson.Id}",
            new LessonRequest(level.Id, "LESSON01", "Greetings Updated", "Basic greetings"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PutAsJsonAsync(
            $"/api/lessons/{lesson.Id}",
            new LessonRequest(otherLevel.Id, "LESSON01", "Greetings Updated", "Basic greetings"))).StatusCode);

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
