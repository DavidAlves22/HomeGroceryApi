using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PainelAdminContaCriadaEUltimoAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoAcessoEm",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            // Contas que já existiam: o cadastro cria o grupo pessoal no mesmo instante, então a entrada mais antiga
            // no primeiro grupo é a melhor data de criação disponível. Sem vínculo nenhum, usa o momento da migration.
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers" u
                SET "CriadoEm" = COALESCE((SELECT MIN(m."EntrouEm") FROM membros_do_grupo m WHERE m."UsuarioId" = u."Id"), now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UltimoAcessoEm",
                table: "AspNetUsers");
        }
    }
}
