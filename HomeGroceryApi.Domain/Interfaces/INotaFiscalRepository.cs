using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface INotaFiscalRepository
{
    Task<List<VinculoDeProdutoDaNota>> ListarVinculosAsync(Guid grupoId, string cnpj, CancellationToken ct);
    Task AdicionarVinculoAsync(VinculoDeProdutoDaNota vinculo, CancellationToken ct);
    Task<bool> ExisteNotaAsync(Guid grupoId, string impressao, CancellationToken ct);
    Task<bool> ExisteNotaPorChaveAsync(Guid grupoId, string chaveDeAcesso, CancellationToken ct);
    Task AdicionarNotaAsync(NotaImportada nota, CancellationToken ct);
}
