using System.Text;
using System.Threading.RateLimiting;
using HomeGroceryApi.Application.Opcoes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace HomeGroceryApi.CrossCutting;

public static class ConfiguracoesIniciais
{
    public const string PoliticaDeCors = "PwaHomeGrocery";
    public const string LimiteDeAutenticacao = "autenticacao";

    public static IServiceCollection AddConfiguracoesIniciais(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var jwt = configuracao.GetSection(JwtOpcoes.Secao).Get<JwtOpcoes>() ?? new JwtOpcoes();
        if (jwt.ChaveSecreta.Length < 32) throw new InvalidOperationException("Jwt:ChaveSecreta precisa ter ao menos 32 caracteres.");

        servicos.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            // Mantém os nomes originais das claims ("sub", "grupo_id") em vez de renomeá-las para URIs longas.
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Emissor,
                ValidAudience = jwt.Audiencia,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ChaveSecreta)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
        servicos.AddAuthorization();

        // A PWA roda em outro domínio (Vercel), então a API precisa autorizar essas origens.
        var origens = configuracao.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
        servicos.AddCors(o => o.AddPolicy(PoliticaDeCors, p => p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));

        // Freia tentativa de adivinhar senha: 10 chamadas por minuto por IP nos endpoints de autenticação.
        var tentativasPorMinuto = configuracao.GetValue("Seguranca:TentativasDeAutenticacaoPorMinuto", 10);
        servicos.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LimiteDeAutenticacao, contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = tentativasPorMinuto, Window = TimeSpan.FromMinutes(1) }));
        });

        servicos.AddEndpointsApiExplorer();
        servicos.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "HomeGrocery API", Version = "v1" });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] },
            });
        });

        return servicos;
    }
}
