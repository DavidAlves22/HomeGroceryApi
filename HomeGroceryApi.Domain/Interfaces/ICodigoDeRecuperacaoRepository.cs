using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface ICodigoDeRecuperacaoRepository
{
    Task AdicionarAsync(CodigoDeRecuperacao codigo, CancellationToken ct);

    /// <summary>O código mais recente que ainda vale (não usado, não vencido, sem excesso de tentativas).</summary>
    Task<CodigoDeRecuperacao?> ObterAtivoMaisRecenteAsync(Guid usuarioId, DateTime agora, CancellationToken ct);

    Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desde, CancellationToken ct);

    /// <summary>Pedir um código novo cancela os anteriores: só o último enviado vale.</summary>
    Task InvalidarAtivosAsync(Guid usuarioId, DateTime agora, CancellationToken ct);
}
