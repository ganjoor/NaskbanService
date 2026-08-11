using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class FlutterClientSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PDFUserBookmarks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "PDFUserBookmarks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "PDFShelves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFShelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFShelves_AspNetUsers_RAppUserId",
                        column: x => x.RAppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PDFStudyLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookId = table.Column<int>(type: "int", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFStudyLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFStudyLogEntries_AspNetUsers_RAppUserId",
                        column: x => x.RAppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFStudyLogEntries_PDFBooks_PDFBookId",
                        column: x => x.PDFBookId,
                        principalTable: "PDFBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PDFShelfBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFShelfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PDFBookId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFShelfBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PDFShelfBooks_PDFBooks_PDFBookId",
                        column: x => x.PDFBookId,
                        principalTable: "PDFBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PDFShelfBooks_PDFShelves_PDFShelfId",
                        column: x => x.PDFShelfId,
                        principalTable: "PDFShelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PDFShelfBooks_PDFBookId",
                table: "PDFShelfBooks",
                column: "PDFBookId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFShelfBooks_PDFShelfId",
                table: "PDFShelfBooks",
                column: "PDFShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFShelves_RAppUserId",
                table: "PDFShelves",
                column: "RAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFStudyLogEntries_PDFBookId",
                table: "PDFStudyLogEntries",
                column: "PDFBookId");

            migrationBuilder.CreateIndex(
                name: "IX_PDFStudyLogEntries_RAppUserId",
                table: "PDFStudyLogEntries",
                column: "RAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFShelfBooks");

            migrationBuilder.DropTable(
                name: "PDFStudyLogEntries");

            migrationBuilder.DropTable(
                name: "PDFShelves");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PDFUserBookmarks");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "PDFUserBookmarks");
        }
    }
}
