using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class LojaConfiguracao : IEntityTypeConfiguration<Loja>
{
    public void Configure(EntityTypeBuilder<Loja> builder)
    {
        builder.ToTable("lojas");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Nome).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Versao).IsConcurrencyToken();
        builder.Property(l => l.Cnpj).HasMaxLength(14);
        builder.Property(l => l.Compartilhada).HasDefaultValue(true);
        builder.HasIndex(l => l.OrigemId);
        builder.Property(l => l.Cidade).HasMaxLength(80);
        builder.Property(l => l.CidadeBusca).HasMaxLength(80);
        builder.HasIndex(l => l.CidadeBusca);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(l => l.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.GrupoId);
    }
}

public sealed class SecaoConfiguracao : IEntityTypeConfiguration<Secao>
{
    public void Configure(EntityTypeBuilder<Secao> builder)
    {
        builder.ToTable("secoes");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Nome).HasMaxLength(40).IsRequired();
        builder.Property(s => s.NomeLadoB).HasMaxLength(40);
        builder.HasOne<Loja>().WithMany().HasForeignKey(s => s.LojaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.GrupoId, s.LojaId });
    }
}

public sealed class SecaoCategoriaConfiguracao : IEntityTypeConfiguration<SecaoCategoria>
{
    public void Configure(EntityTypeBuilder<SecaoCategoria> builder)
    {
        builder.ToTable("secao_categorias");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Lado).HasConversion<string>().HasMaxLength(1);
        builder.HasOne<Secao>().WithMany().HasForeignKey(v => v.SecaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Categoria>().WithMany().HasForeignKey(v => v.CategoriaId).OnDelete(DeleteBehavior.Restrict);

        // Sem unicidade por loja+categoria: a mesma categoria pode estar em várias gôndolas. Só não repete no mesmo lado da mesma seção.
        builder.HasIndex(v => new { v.SecaoId, v.CategoriaId, v.Lado }).IsUnique();
        builder.HasIndex(v => new { v.GrupoId, v.LojaId });
    }
}

public sealed class LocalDoProdutoConfiguracao : IEntityTypeConfiguration<LocalDoProduto>
{
    public void Configure(EntityTypeBuilder<LocalDoProduto> builder)
    {
        builder.ToTable("locais_do_produto");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Chave).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Lado).HasConversion<string>().HasMaxLength(1);
        builder.HasOne<Loja>().WithMany().HasForeignKey(l => l.LojaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Secao>().WithMany().HasForeignKey(l => l.SecaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.LojaId, l.Chave, l.SecaoId, l.Lado }).IsUnique();
        builder.HasIndex(l => new { l.GrupoId, l.LojaId });
    }
}
