using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerformansIndeksleriVeAxtaris : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Axtaris",
                table: "Xercler",
                type: "TEXT",
                maxLength: 900,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Axtaris",
                table: "Xercler",
                column: "Axtaris");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Kategoriya",
                table: "Xercler",
                column: "Kategoriya");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Qrup",
                table: "Xercler",
                column: "Qrup");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Tarix_Id",
                table: "Xercler",
                columns: new[] { "Tarix", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Kreditler_Status",
                table: "Kreditler",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KreditEmeliyyatlari_CreditId_Tarix",
                table: "KreditEmeliyyatlari",
                columns: new[] { "CreditId", "Tarix" });

            migrationBuilder.CreateIndex(
                name: "IX_KreditEmeliyyatlari_Nov",
                table: "KreditEmeliyyatlari",
                column: "Nov");

            migrationBuilder.CreateIndex(
                name: "IX_KreditEmeliyyatlari_Tarix",
                table: "KreditEmeliyyatlari",
                column: "Tarix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Xercler_Axtaris",
                table: "Xercler");

            migrationBuilder.DropIndex(
                name: "IX_Xercler_Kategoriya",
                table: "Xercler");

            migrationBuilder.DropIndex(
                name: "IX_Xercler_Qrup",
                table: "Xercler");

            migrationBuilder.DropIndex(
                name: "IX_Xercler_Tarix_Id",
                table: "Xercler");

            migrationBuilder.DropIndex(
                name: "IX_Kreditler_Status",
                table: "Kreditler");

            migrationBuilder.DropIndex(
                name: "IX_KreditEmeliyyatlari_CreditId_Tarix",
                table: "KreditEmeliyyatlari");

            migrationBuilder.DropIndex(
                name: "IX_KreditEmeliyyatlari_Nov",
                table: "KreditEmeliyyatlari");

            migrationBuilder.DropIndex(
                name: "IX_KreditEmeliyyatlari_Tarix",
                table: "KreditEmeliyyatlari");

            migrationBuilder.DropColumn(
                name: "Axtaris",
                table: "Xercler");
        }
    }
}
