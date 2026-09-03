using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class RevIndexChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PDFBookReviews_PDFBookId_UserId",
                table: "PDFBookReviews");

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviews_PDFBookId_UserId",
                table: "PDFBookReviews",
                columns: new[] { "PDFBookId", "UserId" },
                unique: true,
                filter: "[Status] = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PDFBookReviews_PDFBookId_UserId",
                table: "PDFBookReviews");

            migrationBuilder.CreateIndex(
                name: "IX_PDFBookReviews_PDFBookId_UserId",
                table: "PDFBookReviews",
                columns: new[] { "PDFBookId", "UserId" },
                unique: true);
        }
    }
}
