namespace CmsEdu.Application.Authentication;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
