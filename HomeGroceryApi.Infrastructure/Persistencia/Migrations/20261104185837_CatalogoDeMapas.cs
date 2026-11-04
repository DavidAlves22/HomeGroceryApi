using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroceryApi.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoDeMapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Compartilhada",
                table: "lojas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrigemId",
                table: "lojas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lojas_OrigemId",
                table: "lojas",
                column: "OrigemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lojas_OrigemId",
                table: "lojas");

            migrationBuilder.DropColumn(
                name: "Compartilhada",
                table: "lojas");

            migrationBuilder.DropColumn(
                name: "OrigemId",
                table: "lojas");
        }
    }
}
