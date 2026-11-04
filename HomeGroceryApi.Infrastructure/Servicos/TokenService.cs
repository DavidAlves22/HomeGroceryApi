using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Opcoes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HomeGroceryApi.Infrastructure.Servicos;

public sealed class TokenService : ITokenService
{
    public const string ClaimDoGrupo = "grupo_id";
    public const string ClaimDoPapel = "role";
    public const string PapelAdmin = "Admin";

    private readonly JwtOpcoes _opcoes;
    private readonly IRelogio _relogio;
    private readonly IAdministradores _administradores;

    public TokenService(IOptions<JwtOpcoes> opcoes, IRelogio relogio, IAdministradores administradores)
    {
        _opcoes = opcoes.Value;
        _relogio = relogio;
        _administradores = administradores;
    }

    public (string Token, DateTime ExpiraEm) GerarTokenDeAcesso(Guid usuarioId, string email, Guid grupoId)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.ChaveSecreta));
        var expiraEm = _relogio.AgoraUtc.AddMinutes(_opcoes.MinutosDeValidadeDoToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimDoGrupo, grupoId.ToString()),
        };

        // O papel nasce aqui, no servidor, a partir da configuração; nada vindo do cliente chega a este ponto.
        if (_administradores.EhAdministrador(email)) claims.Add(new Claim(ClaimDoPapel, PapelAdmin));

        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descritor)), expiraEm);
    }

    public (string Token, DateTime ExpiraEm) GerarTokenDeRenovacao()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, _relogio.AgoraUtc.AddDays(_opcoes.DiasDeValidadeDoTokenDeRenovacao));
    }

    public string CalcularHash(string token)
    {
        return Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
