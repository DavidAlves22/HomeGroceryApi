using Google.Apis.Auth;
using HomeGroceryApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>
/// Confere o ID token do Google Identity Services (assinatura contra as chaves públicas do Google, emissor, validade e
/// audiência). `Google:ClienteId` aceita vários Client IDs separados por vírgula; sem nenhum, nenhum token é válido:
/// nunca se confia por falta de configuração.
/// </summary>
public sealed class ValidadorDeLoginGoogle : IValidadorDeLoginGoogle
{
    private readonly string[] _clientesValidos;

    public ValidadorDeLoginGoogle(IConfiguration configuracao)
    {
        _clientesValidos = (configuracao["Google:ClienteId"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public string? ClienteId => _clientesValidos.FirstOrDefault();

    public async Task<IdentidadeGoogle?> ValidarAsync(string idToken, CancellationToken ct)
    {
        if (_clientesValidos.Length == 0 || string.IsNullOrWhiteSpace(idToken)) return null;

        try
        {
            var dados = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = _clientesValidos });

            // E-mail não verificado no Google não prova quem é a pessoa: na dúvida, recusa.
            return dados.EmailVerified && !string.IsNullOrWhiteSpace(dados.Email) ? new IdentidadeGoogle(dados.Email, dados.Name) : null;
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
