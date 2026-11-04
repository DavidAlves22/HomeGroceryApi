using System.Collections.Concurrent;
using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Tests.Fakes;

/// <summary>Guarda os avisos que seriam enviados, para os testes conferirem quem recebeu o quê (sem falar com push de verdade).</summary>
public sealed class NotificadorDePushDeTeste : INotificadorDePush
{
    private readonly ConcurrentQueue<(IReadOnlyCollection<Guid> Usuarios, NotificacaoDePush Notificacao)> _enviados = new();

    public bool Habilitado => true;
    public string? ChavePublica => "chave-publica-de-teste";

    public IReadOnlyList<(IReadOnlyCollection<Guid> Usuarios, NotificacaoDePush Notificacao)> Enviados => _enviados.ToArray();

    public Task EnviarAsync(IReadOnlyCollection<Guid> usuarios, NotificacaoDePush notificacao, CancellationToken ct)
    {
        _enviados.Enqueue((usuarios.ToArray(), notificacao));
        return Task.CompletedTask;
    }
}
