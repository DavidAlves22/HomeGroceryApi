using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LimparCategoriasExcluidasDosMapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Categorias já excluídas continuavam ligadas às seções e travavam o salvamento do mapa: solta os vínculos antigos.
            migrationBuilder.Sql("DELETE FROM secao_categorias WHERE \"CategoriaId\" IN (SELECT \"Id\" FROM categorias WHERE \"ExcluidoEm\" IS NOT NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
