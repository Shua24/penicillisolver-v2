using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace penicillisolver_v2.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAntibioticAbbreviationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntibioticAbbreviations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpreadsheetUploadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibioticAbbreviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntibioticAbbreviations_SpreadsheetUploads_SpreadsheetUploadId",
                        column: x => x.SpreadsheetUploadId,
                        principalTable: "SpreadsheetUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntibioticAbbreviations_Upload_Abbreviation",
                table: "AntibioticAbbreviations",
                columns: new[] { "SpreadsheetUploadId", "Abbreviation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntibioticAbbreviations");
        }
    }
}
