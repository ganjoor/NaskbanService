using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class VisitsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PDFVisitRecords_RAppUserId",
                table: "PDFVisitRecords");

            migrationBuilder.CreateIndex(
                name: "IX_PDFVisitRecords_RAppUserId_PDFBookId",
                table: "PDFVisitRecords",
                columns: new[] { "RAppUserId", "PDFBookId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PDFVisitRecords_RAppUserId_PDFBookId",
                table: "PDFVisitRecords");

            migrationBuilder.CreateIndex(
                name: "IX_PDFVisitRecords_RAppUserId",
                table: "PDFVisitRecords",
                column: "RAppUserId");
        }
    }
}
