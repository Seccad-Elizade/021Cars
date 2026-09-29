using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiraAndSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SiraNomresi",
                table: "Avtomobiller",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Satislar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MuqavileNomresi = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Mustəri = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: true),
                    SatisQiymeti = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MayaDeyeri = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SatisTarixi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OdenisUsulu = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Satislar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Satislar_Avtomobiller_CarId",
                        column: x => x.CarId,
                        principalTable: "Avtomobiller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Satislar_CarId",
                table: "Satislar",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Satislar_SatisTarixi",
                table: "Satislar",
                column: "SatisTarixi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Satislar");

            migrationBuilder.DropColumn(
                name: "SiraNomresi",
                table: "Avtomobiller");
        }
    }
}
