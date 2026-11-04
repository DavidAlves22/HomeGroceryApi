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
        // Administradores do app: Admin:Emails (variáveis Admin__Emails__0, Admin__Emails__1...). Sem lista, ninguém é admin.
        servicos.Configure<AdminOpcoes>(configuracao.GetSection(AdminOpcoes.Secao));
        servicos.AddSingleton<IAdministradores, Administradores>();
        servicos.AddScoped<IConsultasDeAdmin, ConsultasDeAdmin>();
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
        servicos.AddScoped<IAssinaturaDePushRepository, AssinaturaDePushRepository>();
        servicos.AddScoped<INotaFiscalRepository, NotaFiscalRepository>();
        servicos.AddScoped<ICodigoDeRecuperacaoRepository, CodigoDeRecuperacaoRepository>();

        // Recuperação de senha por e-mail (SMTP). Sem Email:Host e Email:De fica desligada.
        servicos.Configure<EmailOpcoes>(configuracao.GetSection(EmailOpcoes.Secao));
        servicos.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailSmtp>();

        // Login com Google: Google:ClienteId (Client ID Web do Google Cloud). Sem ele o botão não aparece no app.
        servicos.AddSingleton<IValidadorDeLoginGoogle, ValidadorDeLoginGoogle>();

        // Avisos por Web Push (gratuito). Sem as chaves em Push:ChavePublica/ChavePrivada ficam desligados.
        servicos.Configure<PushOpcoes>(configuracao.GetSection(PushOpcoes.Secao));
        servicos.AddHttpClient<Lib.Net.Http.WebPush.PushServiceClient>();
        servicos.AddScoped<INotificadorDePush, NotificadorDePush>();

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
