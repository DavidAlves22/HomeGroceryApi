using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Opcoes;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Autenticacao;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Identity;
using HomeGroceryApi.Infrastructure.Persistencia;
using HomeGroceryApi.Infrastructure.Repositorios;
using HomeGroceryApi.Infrastructure.Servicos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGroceryApi.CrossCutting.IoC;

/// <summary>
/// Composition root: o único lugar que conhece as implementações concretas de cada interface.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Padrao")
            ?? throw new InvalidOperationException("ConnectionStrings:Padrao ausente.");

        servicos.AddDbContext<HomeGroceryDbContext>(o => o.UseNpgsql(StringDeConexao.Normalizar(conexao)));

        servicos.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        }).AddEntityFrameworkStores<HomeGroceryDbContext>();

        servicos.Configure<JwtOpcoes>(configuracao.GetSection(JwtOpcoes.Secao));

        servicos.AddSingleton<IRelogio, Relogio>();
        servicos.AddSingleton<ITokenService, TokenService>();
        servicos.AddScoped<IServicoDeContas, ServicoDeContas>();
        servicos.AddScoped<IGrupoRepository, GrupoRepository>();
        servicos.AddScoped<IConviteRepository, ConviteRepository>();
        servicos.AddSingleton<IGeradorDeCodigo, GeradorDeCodigo>();
        servicos.AddScoped<ITokenDeRenovacaoRepository, TokenDeRenovacaoRepository>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        servicos.AddScoped<ICategoriaRepository, CategoriaRepository>();
        servicos.AddScoped<IProdutoRepository, ProdutoRepository>();
        servicos.AddScoped<IModeloRepository, ModeloRepository>();
        servicos.AddScoped<IListaRepository, ListaRepository>();
        servicos.AddScoped<ILojaRepository, LojaRepository>();

        return servicos;
    }

    /// <summary>
    /// Registra todo caso de uso concreto (qualquer classe em Application.UseCases), assim um caso de uso
    /// novo não depende de alguém lembrar de adicionar uma linha aqui.
    /// </summary>
    public static IServiceCollection AddCasosDeUso(this IServiceCollection servicos)
    {
        var casosDeUso = typeof(CadastrarUsuario).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace is not null && t.Namespace.StartsWith("HomeGroceryApi.Application.UseCases"));

        foreach (var tipo in casosDeUso) servicos.AddScoped(tipo);

        servicos.AddScoped<CatalogoDeProdutos>();
        servicos.AddScoped<ServicoDeGrupoAtivo>();
        return servicos;
    }
}
