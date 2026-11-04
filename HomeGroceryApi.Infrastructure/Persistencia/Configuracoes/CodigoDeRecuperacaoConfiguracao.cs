using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class CodigoDeRecuperacaoConfiguracao : IEntityTypeConfiguration<CodigoDeRecuperacao>
{
    public void Configure(EntityTypeBuilder<CodigoDeRecuperacao> builder)
    {
        builder.ToTable("codigos_de_recuperacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CodigoHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => new { c.UsuarioId, c.CriadoEm });
    }
}
