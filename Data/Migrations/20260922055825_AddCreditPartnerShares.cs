using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditPartnerShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditId",
                table: "TerefdasPaylari",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerefdasPaylari_CreditId",
                table: "TerefdasPaylari",
                column: "CreditId");

            migrationBuilder.AddForeignKey(
                name: "FK_TerefdasPaylari_Kreditler_CreditId",
                table: "TerefdasPaylari",
                column: "CreditId",
                principalTable: "Kreditler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TerefdasPaylari_Kreditler_CreditId",
                table: "TerefdasPaylari");

            migrationBuilder.DropIndex(
                name: "IX_TerefdasPaylari_CreditId",
                table: "TerefdasPaylari");

            migrationBuilder.DropColumn(
                name: "CreditId",
                table: "TerefdasPaylari");
        }
    }
}
