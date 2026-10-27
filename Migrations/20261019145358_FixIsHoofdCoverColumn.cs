using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class FixIsHoofdCoverColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Voeg kolom IsHoofdCover toe als deze nog niet bestaat
            migrationBuilder.Sql("ALTER TABLE GallerijAfbeeldingen ADD COLUMN IsHoofdCover INTEGER NOT NULL DEFAULT 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE GallerijAfbeeldingen DROP COLUMN IsHoofdCover");
        }
    }
}
