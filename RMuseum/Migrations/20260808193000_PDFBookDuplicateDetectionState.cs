using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PDFBookDuplicateDetectionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PDFBookDuplicateDetectionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    LastProcessedTitleBucketKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalTitleBuckets = table.Column<int>(type: "int", nullable: false),
                    ProcessedTitleBuckets = table.Column<int>(type: "int", nullable: false),
                    LastRunStarted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRunUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDFBookDuplicateDetectionStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PDFBookDuplicateDetectionStates");
        }
    }
}
