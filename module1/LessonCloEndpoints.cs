using System.Text.Json;
using CmsEdu.Application.Curriculum;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Module1;

public partial class AssessmentsController
{
    [HttpGet("classes/{id:int}/clo-lessons")]
    public async Task<IActionResult> CloLessons(int id, CancellationToken ct)
    {
        var lop = await CheckClass(id, ct);
        var lessons = await db.Lessons.AsNoTracking().Where(x => x.LevelId == lop.LevelId && x.IsActive).ToListAsync(ct);
        return Ok(LessonOrdering.ByNumber(lessons).Select(x => new { x.Id, x.Name, x.Code }));
    }

    [HttpPost("classes/{id:int}/lessons/{lessonId:int}/clo")]
    [Authorize(Policy = Permissions.Assessments.Manage)]
    public async Task<IActionResult> SaveLessonClo(int id, int lessonId, GenerateLessonClo request,
        [FromServices] IClassChatService knowledge, [FromServices] IKnowledgeDocumentService documents, CancellationToken ct)
    {
        var lop = await CheckClass(id, ct);
        var lesson = await db.Lessons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lessonId && x.LevelId == lop.LevelId && x.IsActive, ct);
        if (lesson == null) return BadRequest(new { message = "Bài học không thuộc chương trình của lớp." });
        if (request.StartPage is < 1 || request.EndPage is < 1 || request.StartPage > request.EndPage)
            return BadRequest(new { message = "Khoảng trang không hợp lệ." });

        var hocLieu = await knowledge.GetClassKnowledgeInfoAsync(id, ct);
        if (!hocLieu.Documents.Any(x => x.Id == request.DocumentId && x.Status == "ready"))
            return BadRequest(new { message = "Học liệu không khả dụng cho lớp này." });

        byte[] bytes;
        string fileName;
        IReadOnlyList<Criterion> criteria;
        try
        {
            (bytes, fileName) = await documents.GenerateCloRubricAsync(request.DocumentId, null, request.StartPage, request.EndPage, ct);
            if (bytes.Length is <= 0 or > 2 * 1024 * 1024)
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Tệp CLO của Module 2 có kích thước không hợp lệ." });
            using var workbook = new MemoryStream(bytes, writable: false);
            criteria = DocTepClo.Doc(workbook);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Module 2 chưa tạo được CLO. Kiểm tra dịch vụ Hỏi đáp học liệu và thử lại." });
        }
        catch (Exception error) when (error is InvalidDataException or IOException or System.Xml.XmlException or InvalidOperationException or ArgumentException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Module 2 trả về tệp CLO không đúng mẫu." });
        }

        var version = new LessonClo
        {
            ClassId = id, LessonId = lessonId, DocumentId = request.DocumentId, FileName = Path.GetFileName(fileName),
            Workbook = bytes, DefinitionJson = JsonSerializer.Serialize(criteria), CreatedAt = DateTime.UtcNow
        };
        db.Add(version);
        await db.SaveChangesAsync(ct);
        return Ok(new { version.Id, LessonName = lesson.Name, CriterionCount = criteria.Count });
    }

    [HttpPost("sessions/{id:int}/lesson-rubric")]
    [Authorize(Policy = Permissions.Assessments.Manage)]
    public async Task<IActionResult> ResolveLessonRubric(int id, CancellationToken ct)
    {
        var session = await FindSession(id, ct);
        if (session.LessonId == null) return NoContent();

        var rubrics = db.Set<AssessmentRubric>().AsNoTracking().Where(x => x.ClassId == session.ClassId && x.LessonCloId != null);
        var existing = await db.Set<Assessment>().AsNoTracking().Where(x => x.SessionId == id && x.Rubric.LessonCloId != null)
            .OrderBy(x => x.Id).Select(x => x.Rubric).FirstOrDefaultAsync(ct);
        if (existing != null) return Ok(Describe(existing));

        var version = await db.Set<LessonClo>().AsNoTracking()
            .Where(x => x.ClassId == session.ClassId && x.LessonId == session.LessonId)
            .OrderByDescending(x => x.Id).Select(x => new { x.Id, x.DefinitionJson }).FirstOrDefaultAsync(ct);
        if (version == null) return NoContent();

        var saved = await rubrics.FirstOrDefaultAsync(x => x.LessonCloId == version.Id, ct);
        if (saved != null) return Ok(Describe(saved));

        var lessonName = await db.Lessons.Where(x => x.Id == session.LessonId).Select(x => x.Name).SingleAsync(ct);
        var criteria = AssessmentMath.AddFixedCriteria(JsonSerializer.Deserialize<Criterion[]>(version.DefinitionJson)!);
        var rubric = new AssessmentRubric
        {
            ClassId = session.ClassId, LessonCloId = version.Id, Name = lessonName[..Math.Min(lessonName.Length, 200)],
            DefinitionJson = JsonSerializer.Serialize(criteria)
        };
        db.Add(rubric);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(rubric).State = EntityState.Detached;
            var concurrent = await rubrics.FirstOrDefaultAsync(x => x.LessonCloId == version.Id, ct);
            if (concurrent == null) throw;
            rubric = concurrent;
        }
        return Ok(Describe(rubric));
    }

    private static object Describe(AssessmentRubric rubric) =>
        new { rubric.Id, rubric.Name, Criteria = JsonSerializer.Deserialize<Criterion[]>(rubric.DefinitionJson) };
}
