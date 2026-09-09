namespace CmsEdu.Application.Authentication;

public record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
