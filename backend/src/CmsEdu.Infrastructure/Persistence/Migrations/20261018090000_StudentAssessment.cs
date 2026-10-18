using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmsEdu.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018090000_StudentAssessment")]
public class StudentAssessment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE AssessmentRubric (
                Id int IDENTITY PRIMARY KEY,
                ClassId int NOT NULL REFERENCES Classes(Id),
                Name nvarchar(200) NOT NULL,
                DefinitionJson nvarchar(max) NOT NULL
            );
            CREATE INDEX IX_AssessmentRubric_ClassId ON AssessmentRubric(ClassId);
            CREATE TABLE Assessment (
                Id int IDENTITY PRIMARY KEY,
                EnrollmentId int NOT NULL REFERENCES Enrollments(Id),
                RubricId int NOT NULL REFERENCES AssessmentRubric(Id),
                Title nvarchar(max) NOT NULL,
                Date date NOT NULL,
                Score decimal(5,2) NOT NULL CHECK (Score >= 0 AND Score <= 10),
                ScoresJson nvarchar(max) NOT NULL,
                CreatedBy nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL,
                Feedback nvarchar(max) NULL,
                FeedbackSource nvarchar(max) NULL,
                ApprovedBy nvarchar(max) NULL,
                ApprovedAt datetime2 NULL
            );
            CREATE INDEX IX_Assessment_EnrollmentId_Date ON Assessment(EnrollmentId, Date);
            CREATE INDEX IX_Assessment_RubricId ON Assessment(RubricId);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Assessment");
        migrationBuilder.DropTable("AssessmentRubric");
    }
}
