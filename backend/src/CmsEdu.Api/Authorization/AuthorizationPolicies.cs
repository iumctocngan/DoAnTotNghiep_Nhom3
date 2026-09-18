namespace CmsEdu.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string AuthSelfService = "Auth.SelfService";
    public const string StaffManage = "Staff.Manage";
    public const string TeacherClassesRead = "Teachers.Classes.Read";
    public const string TeacherScheduleRead = "Teachers.Schedule.Read";
    public const string CatalogRead = "Catalog.Read";
    public const string CatalogManage = "Catalog.Manage";
}
