using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassChatMessages_Classes_ClassId",
                table: "ClassChatMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "ClassChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassChatConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassChatConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassChatConversations_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassChatMessages_ConversationId_CreatedAt",
                table: "ClassChatMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassChatConversations_ClassId_UserId_UpdatedAt",
                table: "ClassChatConversations",
                columns: new[] { "ClassId", "UserId", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassChatMessages_ClassChatConversations_ConversationId",
                table: "ClassChatMessages",
                column: "ConversationId",
                principalTable: "ClassChatConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassChatMessages_Classes_ClassId",
                table: "ClassChatMessages",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassChatMessages_ClassChatConversations_ConversationId",
                table: "ClassChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassChatMessages_Classes_ClassId",
                table: "ClassChatMessages");

            migrationBuilder.DropTable(
                name: "ClassChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ClassChatMessages_ConversationId_CreatedAt",
                table: "ClassChatMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "ClassChatMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassChatMessages_Classes_ClassId",
                table: "ClassChatMessages",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
