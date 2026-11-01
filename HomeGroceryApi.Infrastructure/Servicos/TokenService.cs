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

    private readonly JwtOpcoes _opcoes;
    private readonly IRelogio _relogio;

    public TokenService(IOptions<JwtOpcoes> opcoes, IRelogio relogio)
    {
        _opcoes = opcoes.Value;
        _relogio = relogio;
    }

    public (string Token, DateTime ExpiraEm) GerarTokenDeAcesso(Guid usuarioId, string email, Guid grupoId)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.ChaveSecreta));
        var expiraEm = _relogio.AgoraUtc.AddMinutes(_opcoes.MinutosDeValidadeDoToken);

        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimDoGrupo, grupoId.ToString()),
            ]),
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
