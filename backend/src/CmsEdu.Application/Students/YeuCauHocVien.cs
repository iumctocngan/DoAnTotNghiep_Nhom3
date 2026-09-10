using System.ComponentModel.DataAnnotations;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Students;

public abstract record YeuCauHocVien(string StudentCode, string FullName, DateOnly DateOfBirth,
    Gender? Gender, string? LearningNote) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext nguCanhKiemTra)
    {
        if (string.IsNullOrWhiteSpace(StudentCode) || StudentCode.Length > 50)
            yield return new ValidationResult("Mã học viên bắt buộc và tối đa 50 ký tự.", [nameof(StudentCode)]);
        if (string.IsNullOrWhiteSpace(FullName) || FullName.Length > 100)
            yield return new ValidationResult("Họ tên bắt buộc và tối đa 100 ký tự.", [nameof(FullName)]);
        if (DateOfBirth == default || DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new ValidationResult("Ngày sinh phải hợp lệ và không ở tương lai.", [nameof(DateOfBirth)]);
        if (Gender.HasValue && !Enum.IsDefined(Gender.Value))
            yield return new ValidationResult("Giới tính phải là 1, 2, 3 hoặc null.", [nameof(Gender)]);
        if (LearningNote?.Length > 500)
            yield return new ValidationResult("Lưu ý học tập tối đa 500 ký tự.", [nameof(LearningNote)]);
    }
}
