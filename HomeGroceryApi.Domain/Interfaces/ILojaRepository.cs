using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface ILojaRepository
{
    /// <summary>Mercados do grupo por nome. Com incluirExcluidas traz também os apagados (para mostrar onde itens antigos foram comprados).</summary>
    Task<List<Loja>> ListarAsync(Guid grupoId, bool incluirExcluidas, CancellationToken ct);

    Task<Loja?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarSecoesAsync(Guid grupoId, CancellationToken ct);
    Task<List<Secao>> ListarSecoesAsync(Guid grupoId, Guid lojaId, CancellationToken ct);
    Task<List<SecaoCategoria>> ListarVinculosAsync(Guid grupoId, Guid lojaId, CancellationToken ct);
    Task AdicionarAsync(Loja loja, CancellationToken ct);
    Task AdicionarMapaAsync(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos, CancellationToken ct);
    void RemoverMapa(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos);
}
