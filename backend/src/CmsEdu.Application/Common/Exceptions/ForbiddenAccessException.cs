namespace CmsEdu.Application.Common.Exceptions;
//403
public sealed class ForbiddenAccessException(string message) : Exception(message);
