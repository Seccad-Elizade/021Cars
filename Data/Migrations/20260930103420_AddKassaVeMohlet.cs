using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKassaVeMohlet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KassaHereketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditId = table.Column<int>(type: "INTEGER", nullable: true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Nov = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Kateqoriya = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OdenisUsulu = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KassaHereketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KassaHereketleri_Kreditler_CreditId",
                        column: x => x.CreditId,
                        principalTable: "Kreditler",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KassaHereketleri_Satislar_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Satislar",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OdenisMohletleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Menbe = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreditId = table.Column<int>(type: "INTEGER", nullable: true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Sira = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OdenisUsulu = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Odenilib = table.Column<bool>(type: "INTEGER", nullable: false),
                    OdenilmeTarixi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdenisMohletleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdenisMohletleri_Kreditler_CreditId",
                        column: x => x.CreditId,
                        principalTable: "Kreditler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OdenisMohletleri_Satislar_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Satislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KassaHereketleri_CreditId",
                table: "KassaHereketleri",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_KassaHereketleri_Nov",
                table: "KassaHereketleri",
                column: "Nov");

            migrationBuilder.CreateIndex(
                name: "IX_KassaHereketleri_SaleId",
                table: "KassaHereketleri",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_KassaHereketleri_Tarix",
                table: "KassaHereketleri",
                column: "Tarix");

            migrationBuilder.CreateIndex(
                name: "IX_OdenisMohletleri_CreditId",
                table: "OdenisMohletleri",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_OdenisMohletleri_Odenilib",
                table: "OdenisMohletleri",
                column: "Odenilib");

            migrationBuilder.CreateIndex(
                name: "IX_OdenisMohletleri_SaleId",
                table: "OdenisMohletleri",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_OdenisMohletleri_Tarix",
                table: "OdenisMohletleri",
                column: "Tarix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KassaHereketleri");

            migrationBuilder.DropTable(
                name: "OdenisMohletleri");
        }
    }
}
