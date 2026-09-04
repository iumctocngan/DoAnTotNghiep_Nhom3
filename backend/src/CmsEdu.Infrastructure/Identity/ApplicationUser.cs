using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CmsEdu.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
