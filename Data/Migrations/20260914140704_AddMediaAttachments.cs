using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Senedler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RefType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RefId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    StoredPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Tarix = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Qeyd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Senedler", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Senedler_RefType_RefId",
                table: "Senedler",
                columns: new[] { "RefType", "RefId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Senedler");
        }
    }
}
