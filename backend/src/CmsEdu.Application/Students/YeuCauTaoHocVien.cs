using CmsEdu.Domain.Enums;
namespace CmsEdu.Application.Students;
public record YeuCauTaoHocVien(string StudentCode, string FullName, DateOnly DateOfBirth,
    Gender? Gender, string? LearningNote, int CourseMonths = 1) : YeuCauHocVien(StudentCode, FullName, DateOfBirth, Gender, LearningNote);
