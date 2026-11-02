using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase2Financeiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalGastoFinal",
                table: "listas_de_compras",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoEstimado",
                table: "itens_da_lista",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoUnitario",
                table: "itens_da_lista",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantidadeComprada",
                table: "itens_da_lista",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalGastoFinal",
                table: "listas_de_compras");

            migrationBuilder.DropColumn(
                name: "PrecoEstimado",
                table: "itens_da_lista");

            migrationBuilder.DropColumn(
                name: "PrecoUnitario",
                table: "itens_da_lista");

            migrationBuilder.DropColumn(
                name: "QuantidadeComprada",
                table: "itens_da_lista");
        }
    }
}
