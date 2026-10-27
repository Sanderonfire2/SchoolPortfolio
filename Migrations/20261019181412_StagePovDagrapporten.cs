using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class StagePovDagrapporten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartDatum",
                table: "Stages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PovLocaties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Bedrijf = table.Column<string>(type: "TEXT", nullable: true),
                    Adres = table.Column<string>(type: "TEXT", nullable: true),
                    Periode = table.Column<string>(type: "TEXT", nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PovLocaties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rapporten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StageId = table.Column<int>(type: "INTEGER", nullable: true),
                    PovLocatieId = table.Column<int>(type: "INTEGER", nullable: true),
                    Pad = table.Column<string>(type: "TEXT", nullable: false),
                    Titel = table.Column<string>(type: "TEXT", nullable: true),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rapporten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rapporten_PovLocaties_PovLocatieId",
                        column: x => x.PovLocatieId,
                        principalTable: "PovLocaties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rapporten_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rapporten_Datum",
                table: "Rapporten",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Rapporten_PovLocatieId",
                table: "Rapporten",
                column: "PovLocatieId");

            migrationBuilder.CreateIndex(
                name: "IX_Rapporten_StageId",
                table: "Rapporten",
                column: "StageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rapporten");

            migrationBuilder.DropTable(
                name: "PovLocaties");

            migrationBuilder.DropColumn(
                name: "StartDatum",
                table: "Stages");
        }
    }
}
