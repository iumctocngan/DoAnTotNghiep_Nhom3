using CmsEdu.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.ExceptionHandling;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Xác thực thất bại"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Không có quyền truy cập"),
            NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
            ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
            ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi máy chủ nội bộ")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "Đã xảy ra lỗi không mong muốn trên máy chủ."
                : exception.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
