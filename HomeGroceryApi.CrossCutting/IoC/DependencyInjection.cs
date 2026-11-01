using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Opcoes;
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

        servicos.AddDbContext<HomeGroceryDbContext>(o => o.UseNpgsql(conexao));

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
        servicos.AddScoped<ITokenDeRenovacaoRepository, TokenDeRenovacaoRepository>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();

        return servicos;
    }

    public static IServiceCollection AddCasosDeUso(this IServiceCollection servicos)
    {
        servicos.AddScoped<EmissorDeSessao>();
        servicos.AddScoped<CadastrarUsuario>();
        servicos.AddScoped<FazerLogin>();
        servicos.AddScoped<RenovarSessao>();

        return servicos;
    }
}
