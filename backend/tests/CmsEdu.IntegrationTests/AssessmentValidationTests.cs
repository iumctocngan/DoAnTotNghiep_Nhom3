using CmsEdu.Module1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.IntegrationTests;

public class AssessmentValidationTests
{
    [Fact]
    public void MvcValidatesAssessmentRecordsAndNestedCriteria()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IObjectModelValidator>();

        ModelStateDictionary Validate(object request)
        {
            var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
                new RouteData(), new ActionDescriptor());
            validator.Validate(context, null, "", request);
            return context.ModelState;
        }

        var criterion = new Criterion("Kỹ năng", 100, "CLO1", "Mô tả", ["1", "2", "3", "4", "5"]);
        Assert.True(Validate(new CreateRubric(1, "Rubric", [criterion])).IsValid);
        Assert.Contains("Criteria[0].Name",
            Validate(new CreateRubric(1, "Rubric", [criterion with { Name = "" }])).Keys);
        Assert.False(Validate(new SaveClassGrades([])).IsValid);
        Assert.True(Validate(new SaveClassGrades([new GradeRow(1, [8])])).IsValid);
        Assert.False(Validate(new ApproveFeedback("")).IsValid);
    }
}
