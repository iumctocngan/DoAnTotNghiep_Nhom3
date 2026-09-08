namespace CmsEdu.Application.Common.Exceptions;
//404
public sealed class NotFoundException(string message) : Exception(message);
