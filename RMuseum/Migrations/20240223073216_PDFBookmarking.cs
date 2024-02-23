using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PDFBookmarking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFUserBookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookId = table.Column<int>(type: "int", nullable: true),
                    PageId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFUserBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFUserBookmarks_AspNetUsers_RAppUserId",
                        column: x => x.RAppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFUserBookmarks_PDFBooks_PDFBookId",
                        column: x => x.PDFBookId,
                        principalTable: "PDFBooks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PDFUserBookmarks_PDFPages_PageId",
                        column: x => x.PageId,
                        principalTable: "PDFPages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFUserBookmarks_PageId",
                table: "PDFUserBookmarks",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFUserBookmarks_PDFBookId",
                table: "PDFUserBookmarks",
                column: "PDFBookId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFUserBookmarks_RAppUserId",
                table: "PDFUserBookmarks",
                column: "RAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFUserBookmarks");
        }
    }
}
