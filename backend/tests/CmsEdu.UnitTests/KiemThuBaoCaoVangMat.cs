using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class KiemThuBaoCaoVangMat
{
    [Fact]
    public async Task GetAbsenceReportAsync_GomTheoHocVienVaKhoaHocChiTinhBuoiDaHoanTat()
    {
        await using var testData = await TestData.CreateAsync();
        await testData.AddAttendanceAsync(testData.Class1, testData.Student1, AttendanceStatus.Absent,
            new DateOnly(2026, 9, 8), SessionStatus.Completed);
        await testData.AddAttendanceAsync(testData.Class1, testData.Student1, AttendanceStatus.Absent,
            new DateOnly(2026, 9, 15), SessionStatus.Completed);
        await testData.AddAttendanceAsync(testData.Class1, testData.Student1, AttendanceStatus.Absent,
            new DateOnly(2026, 9, 22), SessionStatus.Scheduled);
        await testData.AddAttendanceAsync(testData.Class1, testData.Student1, AttendanceStatus.Present,
            new DateOnly(2026, 9, 29), SessionStatus.Completed);
        await testData.AddAttendanceAsync(testData.Class2, testData.Student2, AttendanceStatus.Absent,
            new DateOnly(2026, 10, 6), SessionStatus.Completed);
        await testData.DbContext.SaveChangesAsync();

        var service = testData.CreateService(UserRole.Admin, "admin");
        var result = await service.GetAbsenceReportAsync(
            testData.Course1.Id, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 1, 20);

        var item = Assert.Single(result.Items);
        Assert.Equal(testData.Student1.Id, item.StudentId);
        Assert.Equal(testData.Course1.Id, item.CourseId);
        Assert.Equal(2, item.AbsenceCount);

        var classResult = await service.GetAbsenceReportAsync(
            null, testData.Class1.Id, null, null, 1, 20);

        var classItem = Assert.Single(classResult.Items);
        Assert.Equal(testData.Course1.Id, classItem.CourseId);
        Assert.Equal(2, classItem.AbsenceCount);
    }

    [Fact]
    public async Task GetAbsenceReportAsync_GiaoVienChiXemLopDangPhuTrach()
    {
        await using var testData = await TestData.CreateAsync();
        await testData.AddAttendanceAsync(testData.Class1, testData.Student1, AttendanceStatus.Absent,
            new DateOnly(2026, 9, 8), SessionStatus.Completed);
        await testData.AddAttendanceAsync(testData.Class2, testData.Student2, AttendanceStatus.Absent,
            new DateOnly(2026, 9, 15), SessionStatus.Completed);
        await testData.DbContext.SaveChangesAsync();

        var service = testData.CreateService(UserRole.Teacher, "teacher-1");
        var result = await service.GetAbsenceReportAsync(null, null, null, null, 1, 20);

        var item = Assert.Single(result.Items);
        Assert.Equal(testData.Course1.Id, item.CourseId);
        Assert.Equal(1, item.AbsenceCount);
    }

    [Fact]
    public async Task GetAbsenceReportAsync_TuChoiRoleKhongDuocCapQuyen()
    {
        await using var testData = await TestData.CreateAsync();
        var service = testData.CreateService(UserRole.CustomerCare, "care-1");

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetAbsenceReportAsync(null, null, null, null, 1, 20));
    }

    [Fact]
    public async Task GetAbsenceReportAsync_TuChoiKhoangNgayKhongHopLe()
    {
        await using var testData = await TestData.CreateAsync();
        var service = testData.CreateService(UserRole.Admin, "admin");

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetAbsenceReportAsync(
                null, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1), 1, 20));
    }

    private sealed class TestData : IAsyncDisposable
    {
        private TestData(
            AppDbContext dbContext,
            Course course1,
            Course course2,
            Class class1,
            Class class2,
            Student student1,
            Student student2)
        {
            DbContext = dbContext;
            Course1 = course1;
            Course2 = course2;
            Class1 = class1;
            Class2 = class2;
            Student1 = student1;
            Student2 = student2;
        }

        public AppDbContext DbContext { get; }
        public Course Course1 { get; }
        public Course Course2 { get; }
        public Class Class1 { get; }
        public Class Class2 { get; }
        public Student Student1 { get; }
        public Student Student2 { get; }

        public static async Task<TestData> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new AppDbContext(options);

            var course1 = new Course { Code = "C1", Name = "Khóa học 1" };
            var course2 = new Course { Code = "C2", Name = "Khóa học 2" };
            dbContext.Courses.AddRange(course1, course2);
            await dbContext.SaveChangesAsync();

            var level1 = new Level { CourseId = course1.Id, Code = "L1", Name = "Cấp độ 1" };
            var level2 = new Level { CourseId = course2.Id, Code = "L2", Name = "Cấp độ 2" };
            dbContext.Levels.AddRange(level1, level2);
            await dbContext.SaveChangesAsync();

            var class1 = CreateClass("CLS-1", level1.Id, "teacher-1");
            var class2 = CreateClass("CLS-2", level2.Id, "teacher-2");
            dbContext.Classes.AddRange(class1, class2);
            var student1 = new Student { StudentCode = "HV-1", FullName = "Học viên 1" };
            var student2 = new Student { StudentCode = "HV-2", FullName = "Học viên 2" };
            dbContext.Students.AddRange(student1, student2);
            await dbContext.SaveChangesAsync();

            return new TestData(dbContext, course1, course2, class1, class2, student1, student2);
        }

        public async Task AddAttendanceAsync(
            Class classEntity,
            Student student,
            AttendanceStatus attendanceStatus,
            DateOnly sessionDate,
            SessionStatus sessionStatus)
        {
            var enrollment = await DbContext.Enrollments.SingleOrDefaultAsync(item =>
                item.StudentId == student.Id && item.ClassId == classEntity.Id);
            if (enrollment is null)
            {
                enrollment = new Enrollment
                {
                    StudentId = student.Id,
                    ClassId = classEntity.Id,
                    StartDate = sessionDate.AddMonths(-1),
                    Status = EnrollmentStatus.Active
                };
                DbContext.Enrollments.Add(enrollment);
                await DbContext.SaveChangesAsync();
            }

            var session = new Session
            {
                ClassId = classEntity.Id,
                SessionDate = sessionDate,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(19, 0),
                Status = sessionStatus
            };
            DbContext.Sessions.Add(session);
            await DbContext.SaveChangesAsync();

            DbContext.Attendances.Add(new Attendance
            {
                SessionId = session.Id,
                EnrollmentId = enrollment.Id,
                Status = attendanceStatus,
                MarkedBy = "teacher",
                MarkedAt = DateTime.UtcNow
            });
        }

        public IAttendanceAbsenceReportService CreateService(string role, string userId) =>
            new AttendanceAbsenceReportService(DbContext, new TestCurrentUser(role, userId));

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();

        private static Class CreateClass(string code, int levelId, string teacherId) => new()
        {
            ClassCode = code,
            Name = code,
            LevelId = levelId,
            MainTeacherUserId = teacherId,
            Capacity = 10,
            StartDate = new DateOnly(2026, 1, 1),
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(19, 0),
            Status = ClassStatus.Active
        };
    }

    private sealed class TestCurrentUser(string role, string userId) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
