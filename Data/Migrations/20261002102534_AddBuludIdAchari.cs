using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuludIdAchari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "Xercler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "TerefdasPaylari",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "TerefdasOdenisleri",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "Terefdaslar",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "Satislar",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "OdenisMohletleri",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "Kreditler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "KreditEmeliyyatlari",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "KassaHereketleri",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuludId",
                table: "Avtomobiller",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "Xercler");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "TerefdasPaylari");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "TerefdasOdenisleri");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "Terefdaslar");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "OdenisMohletleri");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "Kreditler");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "KreditEmeliyyatlari");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "KassaHereketleri");

            migrationBuilder.DropColumn(
                name: "BuludId",
                table: "Avtomobiller");
        }
    }
}
