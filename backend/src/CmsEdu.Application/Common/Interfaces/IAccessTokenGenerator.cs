using CmsEdu.Application.Authentication;

namespace CmsEdu.Application.Common.Interfaces;

public interface IAccessTokenGenerator
{
    AccessTokenResult Generate(
        string userId,
        string employeeCode,
        string fullName,
        string email,
        string role);
}
