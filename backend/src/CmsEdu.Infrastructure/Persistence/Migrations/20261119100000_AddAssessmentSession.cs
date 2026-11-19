using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmsEdu.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261119100000_AddAssessmentSession")]
public class AddAssessmentSession : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("SessionId", "Assessment", nullable: true);
        migrationBuilder.AddForeignKey("FK_Assessment_Sessions_SessionId", "Assessment", "SessionId", "Sessions", principalColumn: "Id");
        migrationBuilder.CreateIndex("IX_Assessment_SessionId_RubricId_EnrollmentId", "Assessment",
            new[] { "SessionId", "RubricId", "EnrollmentId" }, unique: true, filter: "[SessionId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Assessment_Sessions_SessionId", "Assessment");
        migrationBuilder.DropIndex("IX_Assessment_SessionId_RubricId_EnrollmentId", "Assessment");
        migrationBuilder.DropColumn("SessionId", "Assessment");
    }
}
