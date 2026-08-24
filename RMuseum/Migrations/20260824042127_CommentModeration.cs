using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class CommentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFPageCommentReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFPageCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PDFPageCommentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFPageCommentReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFPageCommentReports_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PDFPageCommentReports_PDFPageComments_PDFPageCommentId",
                        column: x => x.PDFPageCommentId,
                        principalTable: "PDFPageComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageCommentReports_PDFPageCommentId",
                table: "PDFPageCommentReports",
                column: "PDFPageCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageCommentReports_ReporterId",
                table: "PDFPageCommentReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageCommentReports_ReviewerId",
                table: "PDFPageCommentReports",
                column: "ReviewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFPageCommentReports");
        }
    }
}
