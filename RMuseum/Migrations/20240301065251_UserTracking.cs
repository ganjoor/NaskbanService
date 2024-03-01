using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class UserTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFVisitRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PDFBookId = table.Column<int>(type: "int", nullable: true),
                    PDFPageNumber = table.Column<int>(type: "int", nullable: true),
                    SearchTerm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsFullTextSearch = table.Column<bool>(type: "bit", nullable: false),
                    QueryPageNumber = table.Column<int>(type: "int", nullable: true),
                    QueryPageSize = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFVisitRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFVisitRecords_AspNetUsers_RAppUserId",
                        column: x => x.RAppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFVisitRecords_RAppUserId",
                table: "PDFVisitRecords",
                column: "RAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFVisitRecords");
        }
    }
}
