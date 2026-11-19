using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmsEdu.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261120010000_AddLessonClo")]
public class AddLessonClo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("LessonClo", table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            ClassId = table.Column<int>(nullable: false),
            LessonId = table.Column<int>(nullable: false),
            DocumentId = table.Column<Guid>(nullable: false),
            CreatedAt = table.Column<DateTime>(nullable: false),
            FileName = table.Column<string>(maxLength: 255, nullable: false),
            Workbook = table.Column<byte[]>(nullable: false),
            DefinitionJson = table.Column<string>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_LessonClo", x => x.Id);
            table.ForeignKey("FK_LessonClo_Classes_ClassId", x => x.ClassId, "Classes", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_LessonClo_Lessons_LessonId", x => x.LessonId, "Lessons", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_LessonClo_ClassId_LessonId_Id", "LessonClo", new[] { "ClassId", "LessonId", "Id" });
        migrationBuilder.CreateIndex("IX_LessonClo_LessonId", "LessonClo", "LessonId");
        migrationBuilder.AddColumn<int>("LessonCloId", "AssessmentRubric", nullable: true);
        migrationBuilder.CreateIndex("IX_AssessmentRubric_LessonCloId", "AssessmentRubric", "LessonCloId");
        migrationBuilder.CreateIndex("IX_AssessmentRubric_ClassId_LessonCloId", "AssessmentRubric",
            new[] { "ClassId", "LessonCloId" }, unique: true, filter: "[LessonCloId] IS NOT NULL");
        migrationBuilder.AddForeignKey("FK_AssessmentRubric_LessonClo_LessonCloId", "AssessmentRubric", "LessonCloId",
            "LessonClo", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_AssessmentRubric_LessonClo_LessonCloId", "AssessmentRubric");
        migrationBuilder.DropIndex("IX_AssessmentRubric_ClassId_LessonCloId", "AssessmentRubric");
        migrationBuilder.DropIndex("IX_AssessmentRubric_LessonCloId", "AssessmentRubric");
        migrationBuilder.DropColumn("LessonCloId", "AssessmentRubric");
        migrationBuilder.DropTable("LessonClo");
    }
}
