using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class VinculoDeProdutoDaNotaConfiguracao : IEntityTypeConfiguration<VinculoDeProdutoDaNota>
{
    public void Configure(EntityTypeBuilder<VinculoDeProdutoDaNota> builder)
    {
        builder.ToTable("vinculos_de_produto_da_nota");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Cnpj).HasMaxLength(14).IsRequired();
        builder.Property(v => v.Codigo).HasMaxLength(40).IsRequired();
        builder.HasOne<Grupo>().WithMany().HasForeignKey(v => v.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(v => v.ProdutoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.GrupoId, v.Cnpj, v.Codigo }).IsUnique();
    }
}

public sealed class NotaImportadaConfiguracao : IEntityTypeConfiguration<NotaImportada>
{
    public void Configure(EntityTypeBuilder<NotaImportada> builder)
    {
        builder.ToTable("notas_importadas");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Cnpj).HasMaxLength(14).IsRequired();
        builder.Property(n => n.Impressao).HasMaxLength(64).IsRequired();
        builder.Property(n => n.Total).HasPrecision(12, 2);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(n => n.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(n => n.ChaveDeAcesso).HasMaxLength(44);
        builder.HasIndex(n => new { n.GrupoId, n.Impressao }).IsUnique();
        builder.HasIndex(n => new { n.GrupoId, n.ChaveDeAcesso }).IsUnique().HasFilter("\"ChaveDeAcesso\" IS NOT NULL");
    }
}
