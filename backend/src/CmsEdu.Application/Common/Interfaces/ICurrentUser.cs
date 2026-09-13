namespace CmsEdu.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
