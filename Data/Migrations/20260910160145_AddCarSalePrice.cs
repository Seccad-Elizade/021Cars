using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarSalePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SatisQiymeti",
                table: "Avtomobiller",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SatisQiymeti",
                table: "Avtomobiller");
        }
    }
}
