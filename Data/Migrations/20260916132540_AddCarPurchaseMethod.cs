using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <summary>
    /// Avtomobil alışına ÖDƏNİŞ ÜSULU (Nağd / Barter) və
    /// barter təsviri sahələrini əlavə edir.
    /// </summary>
    public partial class AddCarPurchaseMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlisUsulu",
                table: "Avtomobiller",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Nağd");

            migrationBuilder.AddColumn<string>(
                name: "BarterTesviri",
                table: "Avtomobiller",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlisUsulu",
                table: "Avtomobiller");

            migrationBuilder.DropColumn(
                name: "BarterTesviri",
                table: "Avtomobiller");
        }
    }
}
