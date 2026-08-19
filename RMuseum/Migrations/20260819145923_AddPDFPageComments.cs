using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddPDFPageComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFPageComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFPageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InReplyToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFPageComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFPageComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFPageComments_PDFPageComments_InReplyToId",
                        column: x => x.InReplyToId,
                        principalTable: "PDFPageComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PDFPageComments_PDFPages_PDFPageId",
                        column: x => x.PDFPageId,
                        principalTable: "PDFPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageComments_InReplyToId",
                table: "PDFPageComments",
                column: "InReplyToId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageComments_PDFPageId",
                table: "PDFPageComments",
                column: "PDFPageId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageComments_UserId",
                table: "PDFPageComments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFPageComments");
        }
    }
}
