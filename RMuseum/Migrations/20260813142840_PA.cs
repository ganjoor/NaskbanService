using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFPinnedAuthors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFPinnedAuthors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFPinnedAuthors_AspNetUsers_RAppUserId",
                        column: x => x.RAppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFPinnedAuthors_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFPinnedAuthors_AuthorId",
                table: "PDFPinnedAuthors",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFPinnedAuthors_RAppUserId",
                table: "PDFPinnedAuthors",
                column: "RAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFPinnedAuthors");
        }
    }
}
