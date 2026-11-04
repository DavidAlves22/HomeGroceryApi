using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Tests.Fakes;

/// <summary>Aceita tokens no formato "ok|email|nome" (o nome é opcional); qualquer outro é inválido.</summary>
public sealed class ValidadorDeLoginGoogleDeTeste : IValidadorDeLoginGoogle
{
    public string? ClienteId { get; set; } = "cliente-de-teste.apps.googleusercontent.com";

    public static string Token(string email, string? nome = null) => $"ok|{email}|{nome}";

    public Task<IdentidadeGoogle?> ValidarAsync(string idToken, CancellationToken ct)
    {
        var partes = idToken.Split('|');
        IdentidadeGoogle? identidade = partes.Length >= 2 && partes[0] == "ok" ? new IdentidadeGoogle(partes[1], partes.Length > 2 && partes[2].Length > 0 ? partes[2] : null) : null;
        return Task.FromResult(identidade);
    }
}
