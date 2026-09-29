using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePartnerShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SaleId",
                table: "TerefdasPaylari",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerefdasPaylari_SaleId",
                table: "TerefdasPaylari",
                column: "SaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_TerefdasPaylari_Satislar_SaleId",
                table: "TerefdasPaylari",
                column: "SaleId",
                principalTable: "Satislar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TerefdasPaylari_Satislar_SaleId",
                table: "TerefdasPaylari");

            migrationBuilder.DropIndex(
                name: "IX_TerefdasPaylari_SaleId",
                table: "TerefdasPaylari");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "TerefdasPaylari");
        }
    }
}
