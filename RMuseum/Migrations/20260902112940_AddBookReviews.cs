using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddBookReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "PDFBooks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "PDFBooks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PDFBookReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    DislikeCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFBookReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFBookReviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFBookReviews_PDFBooks_PDFBookId",
                        column: x => x.PDFBookId,
                        principalTable: "PDFBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PDFBookReviewVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLike = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFBookReviewVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFBookReviewVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFBookReviewVotes_PDFBookReviews_PDFBookReviewId",
                        column: x => x.PDFBookReviewId,
                        principalTable: "PDFBookReviews",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviews_PDFBookId_UserId",
                table: "PDFBookReviews",
                columns: new[] { "PDFBookId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviews_UserId",
                table: "PDFBookReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviewVotes_PDFBookReviewId_UserId",
                table: "PDFBookReviewVotes",
                columns: new[] { "PDFBookReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviewVotes_UserId",
                table: "PDFBookReviewVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFBookReviewVotes");

            migrationBuilder.DropTable(
                name: "PDFBookReviews");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "PDFBooks");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "PDFBooks");
        }
    }
}
