using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services;

public class SessionService(AppDbContext dbContext, ICurrentUser currentUser) : ISessionService
{
    private const int MaxPageSize = 100;

    // [Danh sách buổi học] Lọc theo lớp, khoảng ngày, phân trang và áp dụng quyền
    public async Task<PagedResult<SessionResponse>> GetSessionsAsync(
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize);
        ValidateDateRange(fromDate, toDate);

        var query = ApplyReadScope(dbContext.Sessions
            .AsNoTracking()
            .Include(session => session.Class)
            .Include(session => session.Lesson)
            .AsQueryable());

        if (classId.HasValue)
        {
            query = query.Where(session => session.ClassId == classId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(session => session.SessionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(session => session.SessionDate <= toDate.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var sessionEntities = await query
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .ThenBy(session => session.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var sessions = sessionEntities.Select(ToResponse).ToList();

        return new PagedResult<SessionResponse>(sessions, page, pageSize, totalItems);
    }

    // [Chi tiết buổi học] Lấy thông tin buổi học theo ID
    public async Task<SessionResponse> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var session = await ApplyReadScope(dbContext.Sessions
            .AsNoTracking()
            .Include(item => item.Class)
            .Include(item => item.Lesson))
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        return ToResponse(session);
    }

    // [Tạo buổi học] Thêm buổi học mới (kiểm tra lớp hoạt động, lịch GV, bài học)
    public async Task<SessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.StartTime, request.EndTime, request.Note);

        var classEntity = await dbContext.Classes
            .SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lớp học.");

        if (classEntity.Status != ClassStatus.Active)
        {
            throw new ConflictException("Chỉ có thể tạo buổi học cho lớp học đang hoạt động.");
        }

        ValidateSessionDate(classEntity, request.SessionDate);
        await EnsureTeacherIsAvailableAsync(
            classEntity.MainTeacherUserId,
            request.SessionDate,
            request.StartTime,
            request.EndTime,
            null,
            cancellationToken);
        var lesson = await GetLessonForClassAsync(request.LessonId, classEntity.LevelId, cancellationToken);
        var session = new Session
        {
            ClassId = classEntity.Id,
            LessonId = lesson?.Id,
            SessionDate = request.SessionDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Note = NormalizeNote(request.Note)
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        session.Class = classEntity;
        session.Lesson = lesson;
        return ToResponse(session);
    }

    // [Cập nhật buổi học] Cập nhật thông tin khi buổi học ở trạng thái Scheduled
    public async Task<SessionResponse> UpdateSessionAsync(
        int id,
        UpdateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.StartTime, request.EndTime, request.Note);

        var session = await dbContext.Sessions
            .Include(item => item.Class)
            .Include(item => item.Lesson)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");

        if (session.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể cập nhật buổi học đang ở trạng thái lên lịch.");
        }

        ValidateSessionDate(session.Class, request.SessionDate);
        await EnsureTeacherIsAvailableAsync(
            session.Class.MainTeacherUserId,
            request.SessionDate,
            request.StartTime,
            request.EndTime,
            session.Id,
            cancellationToken);
        var lesson = await GetLessonForClassAsync(request.LessonId, session.Class.LevelId, cancellationToken);
        session.LessonId = lesson?.Id;
        session.Lesson = lesson;
        session.SessionDate = request.SessionDate;
        session.StartTime = request.StartTime;
        session.EndTime = request.EndTime;
        session.Note = NormalizeNote(request.Note);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    // [Hủy buổi học] Chuyển trạng thái buổi học sang Cancelled
    public async Task<SessionResponse> CancelSessionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var session = await GetTrackedSessionAsync(id, cancellationToken);

        if (session.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể hủy buổi học đang ở trạng thái lên lịch.");
        }

        session.Status = SessionStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    // [Hoàn thành buổi học] Chốt hoàn thành khi đã điểm danh đầy đủ học viên
    public async Task<SessionResponse> CompleteSessionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var session = await GetTrackedSessionAsync(id, cancellationToken);

        if (session.Status != SessionStatus.Scheduled)
        {
            throw new ConflictException("Chỉ có thể hoàn thành buổi học đang ở trạng thái lên lịch.");
        }

        EnsureCanComplete(session);

        var eligibleEnrollmentIds = await dbContext.Enrollments
            .Where(enrollment =>
                enrollment.ClassId == session.ClassId &&
                enrollment.Status == EnrollmentStatus.Active &&
                enrollment.StartDate <= session.SessionDate &&
                (enrollment.EndDate == null || enrollment.EndDate >= session.SessionDate))
            .Select(enrollment => enrollment.Id)
            .ToListAsync(cancellationToken);

        var attendanceEnrollmentIds = await dbContext.Attendances
            .Where(attendance => attendance.SessionId == session.Id)
            .Select(attendance => attendance.EnrollmentId)
            .ToListAsync(cancellationToken);

        var missingEnrollmentIds = eligibleEnrollmentIds.Except(attendanceEnrollmentIds).ToList();
        if (missingEnrollmentIds.Count > 0)
        {
            throw new ConflictException(
                $"Chưa hoàn tất điểm danh cho các mã ghi danh: {string.Join(", ", missingEnrollmentIds)}.");
        }

        session.Status = SessionStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    // [Helper] Kiểm tra và lấy bài học hợp lệ theo cấp độ của lớp
    private async Task<Lesson?> GetLessonForClassAsync(
        int? lessonId,
        int classLevelId,
        CancellationToken cancellationToken)
    {
        if (!lessonId.HasValue)
        {
            return null;
        }

        var lesson = await dbContext.Lessons
            .SingleOrDefaultAsync(item => item.Id == lessonId.Value, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài học.");

        if (lesson.LevelId != classLevelId)
        {
            throw new ValidationException("Bài học phải thuộc cùng cấp độ với lớp học của buổi học.");
        }

        return lesson;
    }

    // [Helper] Lấy entity buổi học có tracking để cập nhật
    private async Task<Session> GetTrackedSessionAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Sessions
            .Include(item => item.Class)
            .Include(item => item.Lesson)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy buổi học.");
    }

    // [Helper] Kiểm tra xung đột lịch dạy của giáo viên phụ trách
    private async Task EnsureTeacherIsAvailableAsync(
        string teacherUserId,
        DateOnly sessionDate,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludedSessionId,
        CancellationToken cancellationToken)
    {
        var hasConflict = await dbContext.Sessions
            .Where(session =>
                session.Class.MainTeacherUserId == teacherUserId &&
                session.SessionDate == sessionDate &&
                session.Status != SessionStatus.Cancelled &&
                (!excludedSessionId.HasValue || session.Id != excludedSessionId.Value) &&
                session.StartTime < endTime &&
                startTime < session.EndTime)
            .AnyAsync(cancellationToken);

        if (hasConflict)
        {
            throw new ConflictException("Giáo viên phụ trách lớp đã có buổi học bị trùng thời gian.");
        }
    }

    // [Helper] Kiểm tra ngày học nằm trong thời gian hoạt động của lớp
    private static void ValidateSessionDate(Class classEntity, DateOnly sessionDate)
    {
        if (sessionDate < classEntity.StartDate ||
            (classEntity.EndDate.HasValue && sessionDate > classEntity.EndDate.Value))
        {
            throw new ValidationException("Ngày học phải nằm trong khoảng thời gian diễn ra lớp học.");
        }
    }

    // [Helper] Kiểm tra quyền hoàn thành buổi học (Admin hoặc GV phụ trách)
    private void EnsureCanComplete(Session session)
    {
        var canComplete = currentUser.Role == UserRole.Admin ||
            (currentUser.Role == UserRole.Teacher && currentUser.UserId == session.Class.MainTeacherUserId);

        if (!canComplete)
        {
            throw new ForbiddenAccessException("Bạn không có quyền hoàn thành buổi học này.");
        }
    }

    // [Helper] Phân quyền phạm vi dữ liệu đọc theo vai trò người dùng
    private IQueryable<Session> ApplyReadScope(IQueryable<Session> query)
    {
        return currentUser.Role switch
        {
            UserRole.Admin => query,
            UserRole.Teacher when !string.IsNullOrWhiteSpace(currentUser.UserId) =>
                query.Where(session => session.Class.MainTeacherUserId == currentUser.UserId),
            UserRole.CustomerCare => query,
            _ => throw new ForbiddenAccessException("Bạn không có quyền xem danh sách buổi học.")
        };
    }

    // [Helper] Kiểm tra tính hợp lệ của thời gian học và ghi chú
    private static void ValidateRequest(TimeOnly startTime, TimeOnly endTime, string? note)
    {
        if (startTime >= endTime)
        {
            throw new ValidationException("Thời gian bắt đầu phải trước thời gian kết thúc.");
        }

        if (note?.Length > 500)
        {
            throw new ValidationException("Ghi chú không được vượt quá 500 ký tự.");
        }
    }

    // [Helper] Kiểm tra tham số phân trang
    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
        }
    }

    // [Helper] Kiểm tra khoảng ngày tìm kiếm hợp lệ
    private static void ValidateDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ValidationException("Từ ngày không được lớn hơn đến ngày.");
        }
    }

    // [Helper] Chuẩn hóa chuỗi ghi chú
    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    // [Helper] Chuyển đổi Session Entity sang SessionResponse DTO
    private static SessionResponse ToResponse(Session session)
    {
        return new SessionResponse(
            session.Id,
            session.ClassId,
            session.Class.ClassCode,
            session.Class.Name,
            session.LessonId,
            session.Lesson?.Code,
            session.Lesson?.Name,
            session.SessionDate,
            session.StartTime,
            session.EndTime,
            session.Status,
            session.Note);
    }
}
