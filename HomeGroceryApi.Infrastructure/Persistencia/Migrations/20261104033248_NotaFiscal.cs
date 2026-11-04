using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "lojas",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notas_importadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Impressao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChaveDeAcesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_importadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notas_importadas_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_de_produto_da_nota",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculos_de_produto_da_nota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vinculos_de_produto_da_nota_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vinculos_de_produto_da_nota_produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notas_importadas_GrupoId_ChaveDeAcesso",
                table: "notas_importadas",
                columns: new[] { "GrupoId", "ChaveDeAcesso" },
                unique: true,
                filter: "\"ChaveDeAcesso\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notas_importadas_GrupoId_Impressao",
                table: "notas_importadas",
                columns: new[] { "GrupoId", "Impressao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_de_produto_da_nota_GrupoId_Cnpj_Codigo",
                table: "vinculos_de_produto_da_nota",
                columns: new[] { "GrupoId", "Cnpj", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_de_produto_da_nota_ProdutoId",
                table: "vinculos_de_produto_da_nota",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notas_importadas");

            migrationBuilder.DropTable(
                name: "vinculos_de_produto_da_nota");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "lojas");
        }
    }
}
