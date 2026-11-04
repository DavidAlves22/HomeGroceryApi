using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CidadeDoMercado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "lojas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CidadeBusca",
                table: "lojas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lojas_CidadeBusca",
                table: "lojas",
                column: "CidadeBusca");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lojas_CidadeBusca",
                table: "lojas");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "lojas");

            migrationBuilder.DropColumn(
                name: "CidadeBusca",
                table: "lojas");
        }
    }
}
