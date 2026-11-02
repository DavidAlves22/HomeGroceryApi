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
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ItemDoModelo> ItensDoModelo => Set<ItemDoModelo>();
    public DbSet<ListaDeCompras> Listas => Set<ListaDeCompras>();
    public DbSet<ItemDaLista> ItensDasListas => Set<ItemDaLista>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeGroceryDbContext).Assembly);
    }
}
