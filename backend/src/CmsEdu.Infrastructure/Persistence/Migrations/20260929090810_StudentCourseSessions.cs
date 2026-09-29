using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentCourseSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseMonths",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RemainingSessions",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_CourseMonths",
                table: "Students",
                sql: "[CourseMonths] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_RemainingSessions",
                table: "Students",
                sql: "[RemainingSessions] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_CourseMonths",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_RemainingSessions",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CourseMonths",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "RemainingSessions",
                table: "Students");
        }
    }
}
