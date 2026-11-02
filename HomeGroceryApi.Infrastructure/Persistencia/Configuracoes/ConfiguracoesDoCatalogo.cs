using HomeGroceryApi.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeGroceryApi.Infrastructure.Persistencia.Configuracoes;

public sealed class ProdutoConfiguracao : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nome).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(500);
        builder.Property(p => p.UnidadePadrao).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(p => p.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Categoria>().WithMany().HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.GrupoId, p.Nome });
    }
}

public sealed class ItemDoModeloConfiguracao : IEntityTypeConfiguration<ItemDoModelo>
{
    public void Configure(EntityTypeBuilder<ItemDoModelo> builder)
    {
        builder.ToTable("itens_do_modelo");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Nome).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Unidade).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.QuantidadePadrao).HasPrecision(12, 3);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(i => i.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Categoria>().WithMany().HasForeignKey(i => i.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.GrupoId, i.Ordem });
    }
}

public sealed class ListaDeComprasConfiguracao : IEntityTypeConfiguration<ListaDeCompras>
{
    public void Configure(EntityTypeBuilder<ListaDeCompras> builder)
    {
        builder.ToTable("listas_de_compras");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Competencia).HasMaxLength(7).IsRequired();
        builder.Property(l => l.Nome).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.TotalGastoFinal).HasPrecision(12, 2);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(l => l.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.GrupoId, l.Competencia });

        // Regra "uma lista principal por mês e grupo" garantida pelo banco, mesmo se duas requisições chegarem juntas.
        builder.HasIndex(l => new { l.GrupoId, l.Competencia }).IsUnique().HasFilter("\"Principal\" = true").HasDatabaseName("ux_lista_principal_por_mes");
    }
}

public sealed class ItemDaListaConfiguracao : IEntityTypeConfiguration<ItemDaLista>
{
    public void Configure(EntityTypeBuilder<ItemDaLista> builder)
    {
        builder.ToTable("itens_da_lista");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.NomeSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(i => i.CategoriaNomeSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(i => i.UnidadeSnapshot).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.QuantidadePlanejada).HasPrecision(12, 3);
        builder.Property(i => i.QuantidadeComprada).HasPrecision(12, 3);
        builder.Property(i => i.PrecoEstimado).HasPrecision(12, 2);
        builder.Property(i => i.PrecoUnitario).HasPrecision(12, 2);
        builder.Property(i => i.PrecoReferencia).HasPrecision(12, 2);

        // ProdutoId e CategoriaId são só referências informativas (sem FK): o histórico vive do snapshot.
        builder.Property(i => i.Versao).IsConcurrencyToken();
        builder.HasOne<ListaDeCompras>().WithMany().HasForeignKey(i => i.ListaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.ListaId, i.Ordem });
        builder.HasIndex(i => i.GrupoId);
    }
}
