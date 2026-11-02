using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase4Grupos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_membros_do_grupo_UsuarioId",
                table: "membros_do_grupo");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "membros_do_grupo",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "convites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CriadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_convites_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_membro_ativo_por_usuario",
                table: "membros_do_grupo",
                column: "UsuarioId",
                filter: "\"Ativo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_membros_do_grupo_GrupoId",
                table: "membros_do_grupo",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_convites_Codigo",
                table: "convites",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_convites_GrupoId",
                table: "convites",
                column: "GrupoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "convites");

            migrationBuilder.DropIndex(
                name: "ix_membro_ativo_por_usuario",
                table: "membros_do_grupo");

            migrationBuilder.DropIndex(
                name: "IX_membros_do_grupo_GrupoId",
                table: "membros_do_grupo");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "membros_do_grupo");

            migrationBuilder.CreateIndex(
                name: "IX_membros_do_grupo_UsuarioId",
                table: "membros_do_grupo",
                column: "UsuarioId");
        }
    }
}
