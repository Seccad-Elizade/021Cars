using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BolguBazasi",
                table: "KreditEmeliyyatlari",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TerefdasBolguTetbiqOlunub",
                table: "KreditEmeliyyatlari",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TerefdasPaylari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Terefdas = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Faiz = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: false),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    QaligPayi = table.Column<bool>(type: "INTEGER", nullable: false),
                    Sira = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerefdasPaylari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerefdasPaylari_KreditEmeliyyatlari_CreditTransactionId",
                        column: x => x.CreditTransactionId,
                        principalTable: "KreditEmeliyyatlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TerefdasPaylari_CreditTransactionId",
                table: "TerefdasPaylari",
                column: "CreditTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerefdasPaylari");

            migrationBuilder.DropColumn(
                name: "BolguBazasi",
                table: "KreditEmeliyyatlari");

            migrationBuilder.DropColumn(
                name: "TerefdasBolguTetbiqOlunub",
                table: "KreditEmeliyyatlari");
        }
    }
}
