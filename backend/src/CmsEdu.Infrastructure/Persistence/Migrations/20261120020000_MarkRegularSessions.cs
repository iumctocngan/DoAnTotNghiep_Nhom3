using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmsEdu.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261120020000_MarkRegularSessions")]
public class MarkRegularSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("IsRegular", "Sessions", nullable: true);
        migrationBuilder.CreateIndex("IX_Sessions_ClassId_SessionDate", "Sessions",
            new[] { "ClassId", "SessionDate" }, unique: true, filter: "[IsRegular] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Sessions_ClassId_SessionDate", "Sessions");
        migrationBuilder.DropColumn("IsRegular", "Sessions");
    }
}
