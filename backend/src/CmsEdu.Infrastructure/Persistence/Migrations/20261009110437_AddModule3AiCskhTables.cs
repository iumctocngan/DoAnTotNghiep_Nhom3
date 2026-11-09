using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsEdu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModule3AiCskhTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CskhChatSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CskhChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CskhChatSessions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CskhInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    GuardianId = table.Column<int>(type: "int", nullable: false),
                    StaffUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    GuardianFeedback = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CskhInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CskhInteractions_Guardians_GuardianId",
                        column: x => x.GuardianId,
                        principalTable: "Guardians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CskhInteractions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentRenewalPredictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    PredictionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RenewalProbability = table.Column<double>(type: "float", nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    DaysUntilPeriodEnd = table.Column<int>(type: "int", nullable: false),
                    TopDriversJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRenewalPredictions", x => x.Id);
                    table.CheckConstraint("CK_SRP_DaysUntilPeriodEnd", "[DaysUntilPeriodEnd] >= 0");
                    table.CheckConstraint("CK_SRP_Probability", "[RenewalProbability] >= 0.0 AND [RenewalProbability] <= 1.0");
                    table.ForeignKey(
                        name: "FK_StudentRenewalPredictions_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRenewalPredictions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CskhChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContextDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedScriptJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CskhChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CskhChatMessages_CskhChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CskhChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CskhChatMessages_SessionId",
                table: "CskhChatMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CskhChatSessions_StudentId",
                table: "CskhChatSessions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CskhChatSessions_UserId",
                table: "CskhChatSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CskhInteractions_CreatedAt",
                table: "CskhInteractions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CskhInteractions_GuardianId",
                table: "CskhInteractions",
                column: "GuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_CskhInteractions_StaffUserId",
                table: "CskhInteractions",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CskhInteractions_StudentId",
                table: "CskhInteractions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRenewalPredictions_EnrollmentId",
                table: "StudentRenewalPredictions",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRenewalPredictions_StudentId",
                table: "StudentRenewalPredictions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRenewalPredictions_StudentId_PredictionDate",
                table: "StudentRenewalPredictions",
                columns: new[] { "StudentId", "PredictionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CskhChatMessages");

            migrationBuilder.DropTable(
                name: "CskhInteractions");

            migrationBuilder.DropTable(
                name: "StudentRenewalPredictions");

            migrationBuilder.DropTable(
                name: "CskhChatSessions");
        }
    }
}
