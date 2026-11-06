using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class MensagemDeContatoConfiguracao : IEntityTypeConfiguration<MensagemDeContato>
{
    public void Configure(EntityTypeBuilder<MensagemDeContato> builder)
    {
        builder.ToTable("mensagens_de_contato");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.NomeDoUsuario).HasMaxLength(120).IsRequired();
        builder.Property(m => m.EmailDoUsuario).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Assunto).HasMaxLength(MensagemDeContato.AssuntoMaximo).IsRequired();
        builder.Property(m => m.Texto).HasMaxLength(MensagemDeContato.TextoMaximo).IsRequired();
        builder.Property(m => m.Resposta).HasMaxLength(MensagemDeContato.RespostaMaxima);
        builder.HasIndex(m => new { m.UsuarioId, m.CriadoEm });
        builder.HasIndex(m => m.Status);
    }
}

public sealed class InformacaoDeContatoConfiguracao : IEntityTypeConfiguration<InformacaoDeContato>
{
    public void Configure(EntityTypeBuilder<InformacaoDeContato> builder)
    {
        builder.ToTable("informacoes_de_contato");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Titulo).HasMaxLength(InformacaoDeContato.TituloMaximo).IsRequired();
        builder.Property(i => i.Descricao).HasMaxLength(InformacaoDeContato.DescricaoMaxima);
        builder.Property(i => i.Valor).HasMaxLength(InformacaoDeContato.ValorMaximo).IsRequired();
    }
}
