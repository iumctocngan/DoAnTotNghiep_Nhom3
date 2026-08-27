namespace CmsEdu.Domain.Enums;

public static class UserRole
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string CustomerCare = "CustomerCare";

    public static readonly IReadOnlyList<string> AllRoles = [Admin, Teacher, Accountant, CustomerCare];
}
