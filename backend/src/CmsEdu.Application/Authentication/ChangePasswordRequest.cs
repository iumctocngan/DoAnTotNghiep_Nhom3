namespace CmsEdu.Application.Authentication;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
