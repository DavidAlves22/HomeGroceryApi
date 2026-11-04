namespace HomeGroceryApi.Application.Interfaces;

/// <summary>O que o Google confirmou sobre a pessoa. O nome só vem se ela o tiver no perfil.</summary>
public sealed record IdentidadeGoogle(string Email, string? Nome);

/// <summary>
/// Valida o ID token que o botão "Entrar com Google" entregou ao app. Devolve nulo para qualquer token inválido
/// (assinatura, emissor, audiência, expiração ou e-mail não verificado) sem dizer o motivo.
/// </summary>
public interface IValidadorDeLoginGoogle
{
    /// <summary>Client ID público que o app usa para montar o botão; nulo = login com Google desligado.</summary>
    string? ClienteId { get; }

    Task<IdentidadeGoogle?> ValidarAsync(string idToken, CancellationToken ct);
}
