using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class SessionServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_RejectsLessonFromAnotherLevel()
    {
        await using var fixture = await SessionFixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateSessionAsync(
            new CreateSessionRequest(
                fixture.Class.Id,
                fixture.OtherLevelLesson.Id,
                fixture.SessionDate,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                null)));

        Assert.Contains("cùng cấp độ", exception.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_RejectsOverlappingTeacherSchedule()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        fixture.Context.Sessions.Add(new Session
        {
            ClassId = fixture.Class.Id,
            SessionDate = fixture.SessionDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0)
        });
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateSessionAsync(
            new CreateSessionRequest(
                fixture.Class.Id,
                null,
                fixture.SessionDate,
                new TimeOnly(9, 30),
                new TimeOnly(10, 30),
                null)));
    }

    [Fact]
    public async Task CancelSessionAsync_ChangesScheduledSessionToCancelled()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await fixture.CreateSessionAsync();

        var result = await fixture.Service.CancelSessionAsync(session.Id);

        Assert.Equal(SessionStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task CompleteSessionAsync_RejectsWhenAttendanceIsMissing()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await fixture.CreateSessionAsync();
        fixture.Context.Enrollments.Add(new Enrollment
        {
            StudentId = 1,
            ClassId = fixture.Class.Id,
            StartDate = fixture.SessionDate.AddDays(-1),
            Status = EnrollmentStatus.Active
        });
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CompleteSessionAsync(session.Id));
    }

    [Fact]
    public async Task CompleteSessionAsync_CompletesWhenAllEligibleEnrollmentsAreMarked()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await fixture.CreateSessionAsync();
        var enrollment = new Enrollment
        {
            StudentId = 1,
            ClassId = fixture.Class.Id,
            StartDate = fixture.SessionDate.AddDays(-1),
            Status = EnrollmentStatus.Active
        };
        fixture.Context.Enrollments.Add(enrollment);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Attendances.Add(new Attendance
        {
            SessionId = session.Id,
            EnrollmentId = enrollment.Id,
            Status = AttendanceStatus.Present,
            MarkedBy = "admin",
            MarkedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CompleteSessionAsync(session.Id);

        Assert.Equal(SessionStatus.Completed, result.Status);
    }

    private sealed class SessionFixture : IAsyncDisposable
    {
        private SessionFixture(AppDbContext context, Class classEntity, Lesson otherLevelLesson)
        {
            Context = context;
            Class = classEntity;
            OtherLevelLesson = otherLevelLesson;
            Service = new SessionService(context, new TestCurrentUser(UserRole.Admin, "admin"));
        }

        public AppDbContext Context { get; }
        public Class Class { get; }
        public Lesson OtherLevelLesson { get; }
        public SessionService Service { get; }
        public DateOnly SessionDate { get; } = new(2026, 9, 10);

        public static async Task<SessionFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new AppDbContext(options);
            var level = new Level { Code = "L1", Name = "Level 1", SortOrder = 1 };
            var otherLevel = new Level { Code = "L2", Name = "Level 2", SortOrder = 1 };
            context.Levels.AddRange(level, otherLevel);
            await context.SaveChangesAsync();

            var classEntity = new Class
            {
                ClassCode = "CLS-001",
                Name = "Class 1",
                LevelId = level.Id,
                MainTeacherUserId = "teacher-1",
                Capacity = 10,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                DayOfWeek = DayOfWeek.Thursday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                Status = ClassStatus.Active
            };
            var otherLevelLesson = new Lesson
            {
                LevelId = otherLevel.Id,
                Code = "L2-01",
                Name = "Other level lesson",
                SortOrder = 1
            };
            context.Classes.Add(classEntity);
            context.Lessons.Add(otherLevelLesson);
            await context.SaveChangesAsync();

            return new SessionFixture(context, classEntity, otherLevelLesson);
        }

        public Task<SessionResponse> CreateSessionAsync()
        {
            return Service.CreateSessionAsync(new CreateSessionRequest(
                Class.Id,
                null,
                SessionDate,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                null));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestCurrentUser(string role, string userId) : ICurrentUser
    {
        public string? UserId => userId;
        public string? EmployeeCode => null;
        public string? Email => null;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
