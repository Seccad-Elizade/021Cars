using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAeroStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XercKataloqu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Teyinat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Qrup = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Kategoriya = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Sira = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XercKataloqu", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XercKataloqu_Teyinat_Qrup_Kategoriya",
                table: "XercKataloqu",
                columns: new[] { "Teyinat", "Qrup", "Kategoriya" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XercKataloqu");
        }
    }
}
