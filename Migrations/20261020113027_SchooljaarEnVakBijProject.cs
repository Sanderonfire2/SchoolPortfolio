using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class SchooljaarEnVakBijProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Schooljaar",
                table: "Projecten",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vak",
                table: "Projecten",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Schooljaar",
                table: "Projecten");

            migrationBuilder.DropColumn(
                name: "Vak",
                table: "Projecten");
        }
    }
}
