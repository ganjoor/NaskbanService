using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class CommentImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HighlightHeight",
                table: "PDFPageComments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HighlightWidth",
                table: "PDFPageComments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HighlightX",
                table: "PDFPageComments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HighlightY",
                table: "PDFPageComments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "PDFPageComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PDFPageComments_ImageId",
                table: "PDFPageComments",
                column: "ImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_PDFPageComments_GeneralImages_ImageId",
                table: "PDFPageComments",
                column: "ImageId",
                principalTable: "GeneralImages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PDFPageComments_GeneralImages_ImageId",
                table: "PDFPageComments");

            migrationBuilder.DropIndex(
                name: "IX_PDFPageComments_ImageId",
                table: "PDFPageComments");

            migrationBuilder.DropColumn(
                name: "HighlightHeight",
                table: "PDFPageComments");

            migrationBuilder.DropColumn(
                name: "HighlightWidth",
                table: "PDFPageComments");

            migrationBuilder.DropColumn(
                name: "HighlightX",
                table: "PDFPageComments");

            migrationBuilder.DropColumn(
                name: "HighlightY",
                table: "PDFPageComments");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "PDFPageComments");
        }
    }
}
