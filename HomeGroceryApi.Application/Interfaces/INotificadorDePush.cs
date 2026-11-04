namespace HomeGroceryApi.Application.Interfaces;

/// <summary>Aviso para o celular/computador, mesmo com o app fechado. `Tag` junta avisos repetidos num só.</summary>
public sealed record NotificacaoDePush(string Titulo, string Corpo, string Url, string Tag);

/// <summary>
/// Envia avisos por Web Push (gratuito, sem serviço de terceiros além do push do próprio navegador).
/// Sem as chaves configuradas fica desligado: `Habilitado` é falso e nada é enviado.
/// </summary>
public interface INotificadorDePush
{
    bool Habilitado { get; }
    string? ChavePublica { get; }
    Task EnviarAsync(IReadOnlyCollection<Guid> usuarios, NotificacaoDePush notificacao, CancellationToken ct);
}
