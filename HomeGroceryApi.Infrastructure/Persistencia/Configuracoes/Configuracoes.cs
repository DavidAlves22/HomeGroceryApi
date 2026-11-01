using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class GrupoConfiguracao : IEntityTypeConfiguration<Grupo>
{
    public void Configure(EntityTypeBuilder<Grupo> builder)
    {
        builder.ToTable("grupos");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Nome).HasMaxLength(120).IsRequired();
    }
}

public sealed class MembroDoGrupoConfiguracao : IEntityTypeConfiguration<MembroDoGrupo>
{
    public void Configure(EntityTypeBuilder<MembroDoGrupo> builder)
    {
        builder.ToTable("membros_do_grupo");
        builder.HasKey(m => new { m.GrupoId, m.UsuarioId });
        builder.HasOne<Grupo>().WithMany().HasForeignKey(m => m.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.UsuarioId);
    }
}

public sealed class CategoriaConfiguracao : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nome).HasMaxLength(60).IsRequired();
        builder.HasOne<Grupo>().WithMany().HasForeignKey(c => c.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.GrupoId, c.Ordem });
    }
}

public sealed class TokenDeRenovacaoConfiguracao : IEntityTypeConfiguration<TokenDeRenovacao>
{
    public void Configure(EntityTypeBuilder<TokenDeRenovacao> builder)
    {
        builder.ToTable("tokens_de_renovacao");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Hash).HasMaxLength(88).IsRequired();
        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasIndex(t => t.UsuarioId);
    }
}
