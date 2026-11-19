using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using CmsEdu.Module1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.IntegrationTests;

public class LessonCloAssessmentTests
{
    [KiemThuSqlServer]
    public async Task ResolvesStoredLessonCloAndKeepsScoredVersionInReport()
    {
        await using var app = new AuthenticationApiTests.AuthenticationTestApp();
        await app.InitializeAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Select(x => x.Id).SingleAsync();
        var date = new DateOnly(2026, 9, 6);
        var level = new Level { Code = "CD1", Name = "Cấp độ 1", Course = new Course { Code = "KH1", Name = "Toán" } };
        var lesson = new Lesson { Level = level, Code = "BH1", Name = "Chia hình", Objective = "Chia hình thành phần bằng nhau" };
        var start = new TimeOnly(8, 0);
        var end = new TimeOnly(9, 0);
        var lop = new Class { Level = level, Name = "Lớp thử", ClassCode = "L1", Capacity = 10, MainTeacherUserId = userId,
            StartDate = date, StartTime = start, EndTime = end };
        var session = new Session { Class = lop, Lesson = lesson, SessionDate = date, StartTime = start, EndTime = end };
        var enrollment = new Enrollment { Class = lop, StartDate = date,
            Student = new Student { FullName = "Học sinh thử", StudentCode = "HS1", DateOfBirth = new DateOnly(2015, 1, 1), CourseMonths = 3 } };
        db.Add(new Attendance { Session = session, Enrollment = enrollment, MarkedAt = DateTime.UtcNow, MarkedBy = userId });
        await db.SaveChangesAsync();

        async Task SaveClo(string name)
        {
            db.Add(new LessonClo { ClassId = lop.Id, LessonId = lesson.Id, DocumentId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow, FileName = "CLO.xlsx", Workbook = [1],
                DefinitionJson = JsonSerializer.Serialize(new[] { new Criterion(name, 100, "CLO1", "Chia hình") }) });
            await db.SaveChangesAsync();
        }
        var doc = new Document { ClassId = lop.Id, Scope = KnowledgeScope.Class, Title = "Chia hình",
            FileName = "BaiHoc.pdf", StoragePath = "BaiHoc.pdf", FileSize = 1, DocumentType = "pdf", Status = "ready" };
        db.Add(doc);
        await db.SaveChangesAsync();

        var workbook = MakeWorkbook();
        var feedback = new FeedbackResponse();
        using var appWithModule2 = app.WithWebHostBuilder(builder => builder
            .UseSetting("AssessmentAI:ApiKey", "synthetic_testing_key")
            .ConfigureTestServices(services =>
        {
            services.AddHttpClient<IRagModuleClient, RagModuleClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new WorkbookResponse(workbook));
            services.AddHttpClient("AssessmentAI").ConfigurePrimaryHttpMessageHandler(() => feedback);
        }));
        using var client = appWithModule2.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(AuthenticationApiTests.AuthenticationTestApp.Email, AuthenticationApiTests.AuthenticationTestApp.Password));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var generated = await client.PostAsJsonAsync($"/api/assessments/classes/{lop.Id}/lessons/{lesson.Id}/clo",
            new GenerateLessonClo(doc.Id, 1, 2));
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var source = await db.Set<LessonClo>().AsNoTracking().SingleAsync();
        Assert.Equal(workbook, source.Workbook);
        Assert.Equal("CLO1", Assert.Single(JsonSerializer.Deserialize<Criterion[]>(source.DefinitionJson)!).Clo);

        async Task<RubricResult> Resolve()
        {
            using var response = await client.PostAsync($"/api/assessments/sessions/{session.Id}/lesson-rubric", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<RubricResult>())!;
        }

        var rubric = await Resolve();
        Assert.Equal(4, rubric.Criteria.Length);
        Assert.Equal(100, rubric.Criteria.Sum(x => x.Weight));
        Assert.Equal(rubric.Id, (await Resolve()).Id);

        using var grades = await client.PostAsJsonAsync($"/api/assessments/sessions/{session.Id}/rubrics/{rubric.Id}/grades",
            new SaveClassGrades([new GradeRow(enrollment.Id, [8, 8, 6, 7.5m])]));
        Assert.Equal(HttpStatusCode.NoContent, grades.StatusCode);

        await SaveClo("Phiên bản mới");
        Assert.Equal(rubric.Id, (await Resolve()).Id);

        var assessmentId = await db.Set<Assessment>().AsNoTracking().Select(x => x.Id).SingleAsync();
        using var tooShort = await client.PostAsync($"/api/assessments/{assessmentId}/feedback", null);
        Assert.Equal(HttpStatusCode.BadGateway, tooShort.StatusCode);
        Assert.Null(await db.Set<Assessment>().AsNoTracking().Select(x => x.Feedback).SingleAsync());
        var fullFeedback = string.Join(' ', Enumerable.Repeat("Em đã chia hình thành các phần bằng nhau.", 30));
        feedback.Text = fullFeedback;
        using var withoutName = await client.PostAsync($"/api/assessments/{assessmentId}/feedback", null);
        Assert.Equal(HttpStatusCode.BadGateway, withoutName.StatusCode);
        feedback.Text = $"Học sinh thử đã đạt CLO1. {fullFeedback}";
        using var withJargon = await client.PostAsync($"/api/assessments/{assessmentId}/feedback", null);
        Assert.Equal(HttpStatusCode.BadGateway, withJargon.StatusCode);
        feedback.Text = $"Học sinh thử được 7,5 điểm bài tập. {fullFeedback}";
        using var withScore = await client.PostAsync($"/api/assessments/{assessmentId}/feedback", null);
        Assert.Equal(HttpStatusCode.BadGateway, withScore.StatusCode);
        feedback.Text = $"Học sinh thử đã làm tốt phần chia hình. {fullFeedback}";
        using var accepted = await client.PostAsync($"/api/assessments/{assessmentId}/feedback", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/assessments/enrollments/{enrollment.Id}/report");
        Assert.Equal("Lớp thử", report.GetProperty("className").GetString());
        var assessment = report.GetProperty("history")[0];
        Assert.Equal("Chia hình", assessment.GetProperty("lesson").GetProperty("name").GetString());
        Assert.Equal(7.75m, assessment.GetProperty("score").GetDecimal());
        Assert.Equal("SACH-BAI-TAP", assessment.GetProperty("criteria")[3].GetProperty("clo").GetString());
        Assert.Equal(feedback.Text, assessment.GetProperty("feedback").GetString());
    }

    private record RubricResult(int Id, Criterion[] Criteria);

    private static byte[] MakeWorkbook()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string xml)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(xml);
            }
            Add("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Danh sách CLO chi tiết" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            Add("xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
                  <row r="1"><c r="B1" t="inlineStr"><is><t>Mã CLO</t></is></c>
                    <c r="C1" t="inlineStr"><is><t>Chuẩn đầu ra</t></is></c>
                    <c r="D1" t="inlineStr"><is><t>Tiêu chí đánh giá</t></is></c></row>
                  <row r="2"><c r="B2" t="inlineStr"><is><t>CLO1</t></is></c>
                    <c r="C2" t="inlineStr"><is><t>Chia hình thành phần bằng nhau</t></is></c>
                    <c r="D2" t="inlineStr"><is><t>Chia hình</t></is></c></row>
                </sheetData></worksheet>
                """);
        }
        return output.ToArray();
    }

    private sealed class WorkbookResponse(byte[] workbook) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/v1/clo-rubric/generate", request.RequestUri?.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(workbook) });
        }
    }

    private sealed class FeedbackResponse : HttpMessageHandler
    {
        public string Text { get; set; } = "Nhận xét quá ngắn.";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.EndsWith(":generateContent", request.RequestUri?.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var payload = JsonDocument.Parse(body);
            using var input = JsonDocument.Parse(payload.RootElement.GetProperty("contents")[0]
                .GetProperty("parts")[0].GetProperty("text").GetString()!);
            Assert.Equal("Học sinh thử", input.RootElement.GetProperty("StudentName").GetString());
            Assert.StartsWith("Chia hình thành phần bằng nhau", input.RootElement.GetProperty("Criteria")[0]
                .GetProperty("CloDescription").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = Text } } } } }
            }) };
        }
    }
}
