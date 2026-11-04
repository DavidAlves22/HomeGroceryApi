using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface IAssinaturaDePushRepository
{
    Task<AssinaturaDePush?> ObterPorEndpointAsync(string endpoint, CancellationToken ct);
    Task AdicionarAsync(AssinaturaDePush assinatura, CancellationToken ct);
    Task<List<AssinaturaDePush>> ListarDosUsuariosAsync(IReadOnlyCollection<Guid> usuarios, CancellationToken ct);

    /// <summary>Apaga direto no banco (sem unidade de trabalho): usado ao descobrir que o navegador cancelou a assinatura.</summary>
    Task RemoverPorEndpointAsync(string endpoint, Guid? usuarioId, CancellationToken ct);
}
