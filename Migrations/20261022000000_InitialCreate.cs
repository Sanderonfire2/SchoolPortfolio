using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactBerichten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Bericht = table.Column<string>(type: "TEXT", nullable: false),
                    Gelezen = table.Column<bool>(type: "INTEGER", nullable: false),
                    GereviseerdDoorGastschrijver = table.Column<bool>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactBerichten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Opleidingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    NaamNl = table.Column<string>(type: "TEXT", nullable: false),
                    School = table.Column<string>(type: "TEXT", nullable: true),
                    Periode = table.Column<string>(type: "TEXT", nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    KorteIntroductie = table.Column<string>(type: "TEXT", nullable: false),
                    HasVakDag = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opleidingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaginaInhouden",
                columns: table => new
                {
                    Sleutel = table.Column<string>(type: "TEXT", nullable: false),
                    Inhoud = table.Column<string>(type: "TEXT", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaginaInhouden", x => x.Sleutel);
                });

            migrationBuilder.CreateTable(
                name: "Profielen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    AfbeeldingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OverMijTekst = table.Column<string>(type: "TEXT", nullable: false),
                    KorteIntroductie = table.Column<string>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profielen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bedrijf = table.Column<string>(type: "TEXT", nullable: false),
                    Functie = table.Column<string>(type: "TEXT", nullable: true),
                    StartDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EindeDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Opdrachtgever = table.Column<string>(type: "TEXT", nullable: true),
                    Locatie = table.Column<string>(type: "TEXT", nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    Schooljaar = table.Column<string>(type: "TEXT", nullable: true),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Werkervaringen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Bedrijf = table.Column<string>(type: "TEXT", nullable: true),
                    Functie = table.Column<string>(type: "TEXT", nullable: true),
                    Periode = table.Column<string>(type: "TEXT", nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Werkervaringen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projecten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    Categorie = table.Column<string>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: true),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OpleidingEersteGraad = table.Column<string>(type: "TEXT", nullable: true),
                    OpleidingTweedeGraad = table.Column<string>(type: "TEXT", nullable: true),
                    Schooljaar = table.Column<string>(type: "TEXT", nullable: true),
                    Vak = table.Column<string>(type: "TEXT", nullable: true),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projecten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GallerijAfbeeldingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    OpslagPad = table.Column<string>(type: "TEXT", nullable: true),
                    Soort = table.Column<string>(type: "TEXT", nullable: false),
                    IsHoofdCover = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GallerijAfbeeldingen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GallerijAfbeeldingen_Projecten_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projecten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GallerijAfbeeldingen_ProjectId",
                table: "GallerijAfbeeldingen",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GallerijAfbeeldingen");

            migrationBuilder.DropTable(
                name: "ContactBerichten");

            migrationBuilder.DropTable(
                name: "Opleidingen");

            migrationBuilder.DropTable(
                name: "PaginaInhouden");

            migrationBuilder.DropTable(
                name: "Profielen");

            migrationBuilder.DropTable(
                name: "Stages");

            migrationBuilder.DropTable(
                name: "Werkervaringen");

            migrationBuilder.DropTable(
                name: "Projecten");
        }
    }
}
