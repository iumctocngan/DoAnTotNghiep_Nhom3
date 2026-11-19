using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseAndClassChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Documents] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Documents] PRIMARY KEY,
                        [Title] nvarchar(255) NOT NULL,
                        [FileName] nvarchar(255) NOT NULL,
                        [StoragePath] nvarchar(500) NOT NULL,
                        [FileSize] bigint NOT NULL,
                        [DocumentType] varchar(10) NOT NULL,
                        [Status] varchar(20) NOT NULL DEFAULT 'processing',
                        [ErrorMessage] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [UpdatedAt] datetime2 NULL,
                        [Scope] int NOT NULL DEFAULT 1,
                        [CourseId] int NULL,
                        [LevelId] int NULL,
                        [ClassId] int NULL,
                        [UploadedByUserId] nvarchar(450) NULL
                    );
                END
                ELSE IF COL_LENGTH(N'dbo.Documents', N'NotebookId') IS NOT NULL
                     AND COL_LENGTH(N'dbo.Documents', N'Scope') IS NULL
                     AND OBJECT_ID(N'dbo.Notebooks', N'U') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Documents] DROP CONSTRAINT [FK_Documents_Notebooks_NotebookId];
                    DROP INDEX [IX_Documents_NotebookId] ON [dbo].[Documents];
                    ALTER TABLE [dbo].[Documents] ALTER COLUMN [NotebookId] uniqueidentifier NULL;
                    ALTER TABLE [dbo].[Documents] ADD CONSTRAINT [FK_Documents_Notebooks_NotebookId]
                        FOREIGN KEY ([NotebookId]) REFERENCES [dbo].[Notebooks] ([Id]) ON DELETE CASCADE;
                    CREATE INDEX [IX_Documents_NotebookId] ON [dbo].[Documents] ([NotebookId]);

                    ALTER TABLE [dbo].[Documents] ALTER COLUMN [ErrorMessage] nvarchar(max) NULL;
                    ALTER TABLE [dbo].[Documents] ADD [Title] nvarchar(255) NULL,
                        [UpdatedAt] datetime2 NULL, [Scope] int NOT NULL DEFAULT 1,
                        [CourseId] int NULL, [LevelId] int NULL, [ClassId] int NULL,
                        [UploadedByUserId] nvarchar(450) NULL;

                    -- Old notebooks lack a class mapping. Keep their documents out of teacher retrieval.
                    EXEC(N'UPDATE d SET [Title] = d.[FileName], [Scope] = 4,
                                [UploadedByUserId] = n.[TeacherId]
                           FROM [dbo].[Documents] d
                           LEFT JOIN [dbo].[Notebooks] n ON n.[Id] = d.[NotebookId];
                           ALTER TABLE [dbo].[Documents] ALTER COLUMN [Title] nvarchar(255) NOT NULL;');
                END
                ELSE
                    THROW 51000, 'Documents already exists with an unexpected schema. Check the database migration history.', 1;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Classes_ClassId", table: "Documents", column: "ClassId",
                principalTable: "Classes", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Courses_CourseId", table: "Documents", column: "CourseId",
                principalTable: "Courses", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Levels_LevelId", table: "Documents", column: "LevelId",
                principalTable: "Levels", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateTable(
                name: "ClassChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CitationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassChatMessages_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassChatMessages_ClassId_UserId_CreatedAt",
                table: "ClassChatMessages",
                columns: new[] { "ClassId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ClassId",
                table: "Documents",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourseId",
                table: "Documents",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LevelId",
                table: "Documents",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Scope_CourseId_LevelId_ClassId",
                table: "Documents",
                columns: new[] { "Scope", "CourseId", "LevelId", "ClassId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Documents', N'NotebookId') IS NOT NULL
                    THROW 51001, 'Automatic rollback would delete legacy notebook documents.', 1;
                """);
            migrationBuilder.DropTable(name: "ClassChatMessages");
            migrationBuilder.DropTable(name: "Documents");
        }
    }
}
