using CmsEdu.Domain.Enums;
namespace CmsEdu.Application.Students;
public record YeuCauTaoHocVien(string StudentCode, string FullName, DateOnly DateOfBirth,
    Gender? Gender, string? LearningNote) : YeuCauHocVien(StudentCode, FullName, DateOfBirth, Gender, LearningNote);
