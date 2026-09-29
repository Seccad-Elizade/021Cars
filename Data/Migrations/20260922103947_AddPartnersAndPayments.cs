using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnersAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Terefdaslar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Faiz = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: false),
                    QaligPayi = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Sira = table.Column<int>(type: "INTEGER", nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terefdaslar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerefdasOdenisleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Terefdas = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Mebleg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OdenisUsulu = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerefdasOdenisleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Terefdaslar_Ad",
                table: "Terefdaslar",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerefdasOdenisleri_Tarix",
                table: "TerefdasOdenisleri",
                column: "Tarix");

            migrationBuilder.CreateIndex(
                name: "IX_TerefdasOdenisleri_Terefdas",
                table: "TerefdasOdenisleri",
                column: "Terefdas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Terefdaslar");

            migrationBuilder.DropTable(
                name: "TerefdasOdenisleri");
        }
    }
}
