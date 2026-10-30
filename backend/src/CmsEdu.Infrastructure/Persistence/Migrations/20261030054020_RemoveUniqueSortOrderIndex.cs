using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueSortOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Levels_CourseId_SortOrder",
                table: "Levels");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_LevelId_SortOrder",
                table: "Lessons");

            migrationBuilder.CreateIndex(
                name: "IX_Levels_CourseId_SortOrder",
                table: "Levels",
                columns: new[] { "CourseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_LevelId_SortOrder",
                table: "Lessons",
                columns: new[] { "LevelId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Levels_CourseId_SortOrder",
                table: "Levels");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_LevelId_SortOrder",
                table: "Lessons");

            migrationBuilder.CreateIndex(
                name: "IX_Levels_CourseId_SortOrder",
                table: "Levels",
                columns: new[] { "CourseId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_LevelId_SortOrder",
                table: "Lessons",
                columns: new[] { "LevelId", "SortOrder" },
                unique: true);
        }
    }
}
