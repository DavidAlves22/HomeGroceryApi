using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class AssinaturaDePushConfiguracao : IEntityTypeConfiguration<AssinaturaDePush>
{
    public void Configure(EntityTypeBuilder<AssinaturaDePush> builder)
    {
        builder.ToTable("assinaturas_de_push");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Endpoint).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Auth).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => a.Endpoint).IsUnique();
        builder.HasIndex(a => a.UsuarioId);
    }
}
