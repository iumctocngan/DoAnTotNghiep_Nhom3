using CmsEdu.Domain.Enums;
namespace CmsEdu.Application.Students;
public record PhanHoiHocVien(int Id, string StudentCode, string FullName, DateOnly DateOfBirth,
    Gender? Gender, string? LearningNote, bool IsArchived);
