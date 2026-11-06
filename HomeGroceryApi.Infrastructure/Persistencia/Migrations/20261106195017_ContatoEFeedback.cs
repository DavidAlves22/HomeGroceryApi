using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ContatoEFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "informacoes_de_contato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Valor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_informacoes_de_contato", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mensagens_de_contato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeDoUsuario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmailDoUsuario = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Assunto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resposta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RespondidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespostaVistaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagens_de_contato", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mensagens_de_contato_Status",
                table: "mensagens_de_contato",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_mensagens_de_contato_UsuarioId_CriadoEm",
                table: "mensagens_de_contato",
                columns: new[] { "UsuarioId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "informacoes_de_contato");

            migrationBuilder.DropTable(
                name: "mensagens_de_contato");
        }
    }
}
