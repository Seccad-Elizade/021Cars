using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Avtomobiller",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Marka = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    QeydiyyatNisani = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Vin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Il = table.Column<int>(type: "INTEGER", nullable: false),
                    Yurus = table.Column<int>(type: "INTEGER", nullable: false),
                    Yanacaq = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AlisTarixi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AlisSaati = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AlisQiymeti = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    YaradilmaTarixi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avtomobiller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kreditler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MuqavileNomresi = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Mustəri = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: true),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FaizDerecesi = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: false),
                    MuddetAy = table.Column<int>(type: "INTEGER", nullable: false),
                    AylıqOdenis = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BaslamaTarixi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kreditler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kreditler_Avtomobiller_CarId",
                        column: x => x.CarId,
                        principalTable: "Avtomobiller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Xercler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Teyinat = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Qrup = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kategoriya = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: true),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OdenisUsulu = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    YaradilmaTarixi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Xercler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Xercler_Avtomobiller_CarId",
                        column: x => x.CarId,
                        principalTable: "Avtomobiller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KreditEmeliyyatlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditId = table.Column<int>(type: "INTEGER", nullable: true),
                    Nov = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tesvir = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KreditEmeliyyatlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KreditEmeliyyatlari_Kreditler_CreditId",
                        column: x => x.CreditId,
                        principalTable: "Kreditler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Avtomobiller_QeydiyyatNisani",
                table: "Avtomobiller",
                column: "QeydiyyatNisani");

            migrationBuilder.CreateIndex(
                name: "IX_KreditEmeliyyatlari_CreditId",
                table: "KreditEmeliyyatlari",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_Kreditler_CarId",
                table: "Kreditler",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_CarId",
                table: "Xercler",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Tarix",
                table: "Xercler",
                column: "Tarix");

            migrationBuilder.CreateIndex(
                name: "IX_Xercler_Teyinat",
                table: "Xercler",
                column: "Teyinat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KreditEmeliyyatlari");

            migrationBuilder.DropTable(
                name: "Xercler");

            migrationBuilder.DropTable(
                name: "Kreditler");

            migrationBuilder.DropTable(
                name: "Avtomobiller");
        }
    }
}
