using System.ComponentModel.DataAnnotations;

namespace CmsEdu.Application.Guardians;

public sealed record YeuCauNguoiGiamHo(string FullName, string Phone, string? Email, bool IsActive = true) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext nguCanh)
    {
        if (string.IsNullOrWhiteSpace(FullName) || FullName.Length > 100)
            yield return new("Họ tên bắt buộc và tối đa 100 ký tự.", [nameof(FullName)]);
        if (string.IsNullOrWhiteSpace(Phone) || Phone.Length > 20 || !new PhoneAttribute().IsValid(Phone))
            yield return new("Số điện thoại bắt buộc, đúng định dạng và tối đa 20 ký tự.", [nameof(Phone)]);
        if (Email?.Length > 100 || (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email.Trim())))
            yield return new("Email phải đúng định dạng và tối đa 100 ký tự.", [nameof(Email)]);
    }
}

public sealed record YeuCauLienKetNguoiGiamHo(int GuardianId, string Relationship, bool IsPrimary = false) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext nguCanh)
    {
        if (GuardianId < 1) yield return new("Mã người giám hộ phải lớn hơn 0.", [nameof(GuardianId)]);
        if (string.IsNullOrWhiteSpace(Relationship) || Relationship.Length > 50)
            yield return new("Quan hệ bắt buộc và tối đa 50 ký tự.", [nameof(Relationship)]);
    }
}

public sealed record PhanHoiNguoiGiamHo(int Id, string FullName, string Phone, string? Email, bool IsActive);
public sealed record YeuCauCapNhatLienKetNguoiGiamHo(string Relationship, bool IsPrimary = false);
public sealed record PhanHoiLienKetNguoiGiamHo(int GuardianId, string FullName, string Phone, string? Email,
    bool IsActive, string Relationship, bool IsPrimary);
