using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <summary>
    /// BARTER əlaqələrini əlavə edir:
    /// <list type="bullet">
    ///   <item>Avtomobil ALIŞINDA əvəzə verilən maşını avto parkdan seçmək
    ///         üçün <c>BarterCarId</c> və onun dəyəri <c>BarterDeyeri</c>.</item>
    ///   <item>Satışda <c>Barter</c> ödəniş üsulu üçün <c>BarterMebleg</c>
    ///         və alıcının verdiyi maşının təsviri <c>BarterTesviri</c>.</item>
    /// </list>
    /// </summary>
    public partial class AddBarterCarAndSaleBarter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BarterDeyeri",
                table: "Avtomobiller",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BarterCarId",
                table: "Avtomobiller",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BarterMebleg",
                table: "Satislar",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BarterTesviri",
                table: "Satislar",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarterDeyeri",
                table: "Avtomobiller");

            migrationBuilder.DropColumn(
                name: "BarterCarId",
                table: "Avtomobiller");

            migrationBuilder.DropColumn(
                name: "BarterMebleg",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "BarterTesviri",
                table: "Satislar");
        }
    }
}
