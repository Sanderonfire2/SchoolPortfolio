using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class VoegSoortToeAanGallerij : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Soort",
                table: "GallerijAfbeeldingen",
                type: "TEXT",
                nullable: false,
                defaultValue: "afbeelding");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Soort",
                table: "GallerijAfbeeldingen");
        }
    }
}
