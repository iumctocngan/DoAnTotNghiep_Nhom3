using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace CmsEdu.Api.ExceptionHandling;

[AttributeUsage(AttributeTargets.Class)]
public sealed class BoLocKiemTraHocVienAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext nguCanh)
    {
        if (nguCanh.Exception is not ValidationException ngoaiLe) return;
        nguCanh.Result = new BadRequestObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest, Title = "Dữ liệu không hợp lệ",
            Detail = ngoaiLe.Message, Instance = nguCanh.HttpContext.Request.Path
        });
        nguCanh.ExceptionHandled = true;
    }
}
