namespace CmsEdu.Application.Common.Exceptions;
//409
public sealed class ConflictException(string message) : Exception(message);
