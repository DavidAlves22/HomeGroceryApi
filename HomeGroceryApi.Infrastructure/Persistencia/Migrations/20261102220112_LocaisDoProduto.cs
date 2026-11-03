using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LocaisDoProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locais_do_produto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LojaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lado = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locais_do_produto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_locais_do_produto_lojas_LojaId",
                        column: x => x.LojaId,
                        principalTable: "lojas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_locais_do_produto_secoes_SecaoId",
                        column: x => x.SecaoId,
                        principalTable: "secoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_locais_do_produto_GrupoId_LojaId",
                table: "locais_do_produto",
                columns: new[] { "GrupoId", "LojaId" });

            migrationBuilder.CreateIndex(
                name: "IX_locais_do_produto_LojaId_Chave_SecaoId_Lado",
                table: "locais_do_produto",
                columns: new[] { "LojaId", "Chave", "SecaoId", "Lado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locais_do_produto_SecaoId",
                table: "locais_do_produto",
                column: "SecaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "locais_do_produto");
        }
    }
}
