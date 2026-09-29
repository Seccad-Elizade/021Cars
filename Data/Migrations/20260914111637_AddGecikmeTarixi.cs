using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGecikmeTarixi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GecikmeTarixi",
                table: "KreditEmeliyyatlari",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GecikmeTarixi",
                table: "KreditEmeliyyatlari");
        }
    }
}
