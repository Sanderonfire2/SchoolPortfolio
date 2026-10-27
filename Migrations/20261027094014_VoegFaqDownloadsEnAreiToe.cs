using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class VoegFaqDownloadsEnAreiToe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AreiBoeken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Beschrijving = table.Column<string>(type: "TEXT", nullable: true),
                    BestandPad = table.Column<string>(type: "TEXT", nullable: true),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreiBoeken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: true),
                    BestandPad = table.Column<string>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Vraag = table.Column<string>(type: "TEXT", nullable: false),
                    Antwoord = table.Column<string>(type: "TEXT", nullable: false),
                    IsGepubliceerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreiBoeken_Slug",
                table: "AreiBoeken",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreiBoeken");

            migrationBuilder.DropTable(
                name: "DownloadItems");

            migrationBuilder.DropTable(
                name: "FaqItems");
        }
    }
}
