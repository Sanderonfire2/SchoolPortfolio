using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class ContactBerichtBeantwoord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Beantwoord",
                table: "ContactBerichten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Beantwoord",
                table: "ContactBerichten");
        }
    }
}
