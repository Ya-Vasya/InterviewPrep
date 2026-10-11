using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InterviewPrep.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCardsAndReviewLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ease = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Suspended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CardCreated = table.Column<bool>(type: "boolean", nullable: false),
                    ResultDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultIntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Before_DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Before_Ease = table.Column<double>(type: "double precision", nullable: false),
                    Before_IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Before_Lapses = table.Column<int>(type: "integer", nullable: false),
                    Before_LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Before_Repetitions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLogs_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_DueAt",
                table: "Cards",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_QuestionId",
                table: "Cards",
                column: "QuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_CardId",
                table: "ReviewLogs",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_RatedAt",
                table: "ReviewLogs",
                column: "RatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_ReviewId",
                table: "ReviewLogs",
                column: "ReviewId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewLogs");

            migrationBuilder.DropTable(
                name: "Cards");
        }
    }
}
