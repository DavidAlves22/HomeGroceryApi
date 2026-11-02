using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase5MapasEMercados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompradoNaLojaId",
                table: "itens_da_lista",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "LojasSemOItem",
                table: "itens_da_lista",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateTable(
                name: "lojas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Largura = table.Column<int>(type: "integer", nullable: false),
                    Altura = table.Column<int>(type: "integer", nullable: false),
                    EntradaX = table.Column<int>(type: "integer", nullable: false),
                    EntradaY = table.Column<int>(type: "integer", nullable: false),
                    CaixasX = table.Column<int>(type: "integer", nullable: false),
                    CaixasY = table.Column<int>(type: "integer", nullable: false),
                    CaixasLargura = table.Column<int>(type: "integer", nullable: false),
                    CaixasAltura = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    ExcluidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lojas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lojas_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "secoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LojaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NomeLadoB = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Dividida = table.Column<bool>(type: "boolean", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Largura = table.Column<int>(type: "integer", nullable: false),
                    Altura = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_secoes_lojas_LojaId",
                        column: x => x.LojaId,
                        principalTable: "lojas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "secao_categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LojaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lado = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secao_categorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_secao_categorias_categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_secao_categorias_secoes_SecaoId",
                        column: x => x.SecaoId,
                        principalTable: "secoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_itens_da_lista_CompradoNaLojaId",
                table: "itens_da_lista",
                column: "CompradoNaLojaId");

            migrationBuilder.CreateIndex(
                name: "IX_lojas_GrupoId",
                table: "lojas",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_secao_categorias_CategoriaId",
                table: "secao_categorias",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_secao_categorias_GrupoId_LojaId",
                table: "secao_categorias",
                columns: new[] { "GrupoId", "LojaId" });

            migrationBuilder.CreateIndex(
                name: "IX_secao_categorias_SecaoId_CategoriaId_Lado",
                table: "secao_categorias",
                columns: new[] { "SecaoId", "CategoriaId", "Lado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secoes_GrupoId_LojaId",
                table: "secoes",
                columns: new[] { "GrupoId", "LojaId" });

            migrationBuilder.CreateIndex(
                name: "IX_secoes_LojaId",
                table: "secoes",
                column: "LojaId");

            migrationBuilder.AddForeignKey(
                name: "FK_itens_da_lista_lojas_CompradoNaLojaId",
                table: "itens_da_lista",
                column: "CompradoNaLojaId",
                principalTable: "lojas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_itens_da_lista_lojas_CompradoNaLojaId",
                table: "itens_da_lista");

            migrationBuilder.DropTable(
                name: "secao_categorias");

            migrationBuilder.DropTable(
                name: "secoes");

            migrationBuilder.DropTable(
                name: "lojas");

            migrationBuilder.DropIndex(
                name: "IX_itens_da_lista_CompradoNaLojaId",
                table: "itens_da_lista");

            migrationBuilder.DropColumn(
                name: "CompradoNaLojaId",
                table: "itens_da_lista");

            migrationBuilder.DropColumn(
                name: "LojasSemOItem",
                table: "itens_da_lista");
        }
    }
}
