using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCourseAgeRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Courses_AgeRange",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "MaxAge",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "MinAge",
                table: "Courses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "Courses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinAge",
                table: "Courses",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Courses_AgeRange",
                table: "Courses",
                sql: "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
        }
    }
}
