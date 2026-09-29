using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <summary>
    /// Barter SATIŞI nəticəsində parka gələn avtomobili həmin satışla
    /// əlaqələndirmək üçün <c>BarterSaleId</c> sahəsi əlavə edilir.
    /// <para>
    /// Müştəri maşını maşınla (və ya qismən maşınla) ödəyəndə alınan avtomobil
    /// avtomatik parka düşür və onun <b>maya dəyəri = barter dəyəri</b> olur.
    /// Bu sahə həmin əlaqəni izləyir.
    /// </para>
    /// </summary>
    public partial class AddBarterSaleLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BarterSaleId",
                table: "Avtomobiller",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarterSaleId",
                table: "Avtomobiller");
        }
    }
}
