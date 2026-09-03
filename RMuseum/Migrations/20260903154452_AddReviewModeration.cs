using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFBookReviewReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFBookReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFBookReviewReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFBookReviewReports_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PDFBookReviewReports_PDFBookReviews_PDFBookReviewId",
                        column: x => x.PDFBookReviewId,
                        principalTable: "PDFBookReviews",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviewReports_PDFBookReviewId",
                table: "PDFBookReviewReports",
                column: "PDFBookReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviewReports_ReporterId",
                table: "PDFBookReviewReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviewReports_ReviewerId",
                table: "PDFBookReviewReports",
                column: "ReviewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFBookReviewReports");
        }
    }
}
