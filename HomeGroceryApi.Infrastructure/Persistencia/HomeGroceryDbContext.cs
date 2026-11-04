using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Persistencia;

public sealed class HomeGroceryDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public HomeGroceryDbContext(DbContextOptions<HomeGroceryDbContext> opcoes) : base(opcoes)
    {
    }

    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<MembroDoGrupo> Membros => Set<MembroDoGrupo>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<TokenDeRenovacao> TokensDeRenovacao => Set<TokenDeRenovacao>();
    public DbSet<Convite> Convites => Set<Convite>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ItemDoModelo> ItensDoModelo => Set<ItemDoModelo>();
    public DbSet<ListaDeCompras> Listas => Set<ListaDeCompras>();
    public DbSet<ItemDaLista> ItensDasListas => Set<ItemDaLista>();
    public DbSet<Loja> Lojas => Set<Loja>();
    public DbSet<Secao> Secoes => Set<Secao>();
    public DbSet<SecaoCategoria> SecaoCategorias => Set<SecaoCategoria>();
    public DbSet<LocalDoProduto> LocaisDoProduto => Set<LocalDoProduto>();
    public DbSet<AssinaturaDePush> AssinaturasDePush => Set<AssinaturaDePush>();
    public DbSet<VinculoDeProdutoDaNota> VinculosDaNota => Set<VinculoDeProdutoDaNota>();
    public DbSet<NotaImportada> NotasImportadas => Set<NotaImportada>();
    public DbSet<CodigoDeRecuperacao> CodigosDeRecuperacao => Set<CodigoDeRecuperacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeGroceryDbContext).Assembly);
    }
}
