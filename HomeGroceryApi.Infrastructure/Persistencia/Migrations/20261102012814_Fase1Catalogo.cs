using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase1Catalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "categorias",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.CreateTable(
                name: "listas_de_compras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competencia = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Principal = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listas_de_compras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_listas_de_compras_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnidadePadrao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Favorito = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExcluidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_produtos_categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_produtos_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_da_lista",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: true),
                    NomeSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoriaNomeSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UnidadeSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadePlanejada = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comprado = table.Column<bool>(type: "boolean", nullable: false),
                    CompradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompradoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdicionadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_da_lista", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_da_lista_listas_de_compras_ListaId",
                        column: x => x.ListaId,
                        principalTable: "listas_de_compras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_do_modelo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadePadrao = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_do_modelo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_do_modelo_categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_itens_do_modelo_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_itens_do_modelo_produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_itens_da_lista_GrupoId",
                table: "itens_da_lista",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_da_lista_ListaId_Ordem",
                table: "itens_da_lista",
                columns: new[] { "ListaId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_itens_do_modelo_CategoriaId",
                table: "itens_do_modelo",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_do_modelo_GrupoId_Ordem",
                table: "itens_do_modelo",
                columns: new[] { "GrupoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_itens_do_modelo_ProdutoId",
                table: "itens_do_modelo",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "ux_lista_principal_por_mes",
                table: "listas_de_compras",
                columns: new[] { "GrupoId", "Competencia" },
                unique: true,
                filter: "\"Principal\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_CategoriaId",
                table: "produtos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_GrupoId_Nome",
                table: "produtos",
                columns: new[] { "GrupoId", "Nome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_da_lista");

            migrationBuilder.DropTable(
                name: "itens_do_modelo");

            migrationBuilder.DropTable(
                name: "listas_de_compras");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "categorias",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
