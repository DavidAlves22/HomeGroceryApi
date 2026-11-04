using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

/// <summary>Um mapa do catálogo com o que a lista precisa mostrar sem carregar as seções.</summary>
public sealed record ItemDoCatalogo(Loja Loja, int Secoes, int Copias);

public interface ILojaRepository
{
    /// <summary>
    /// Mapas de OUTROS grupos que estão compartilhados, têm cidade, têm seções e não são cópia intocada (uma cópia só entra no catálogo
    /// depois de editada, para não encher de repetidos). Os mais copiados primeiro; busca filtra pelo nome e cidade pela cidade (sem acento nem maiúscula).
    /// </summary>
    Task<List<ItemDoCatalogo>> ListarCatalogoAsync(Guid grupoIdDoUsuario, string? busca, string? cidade, int limite, CancellationToken ct);

    Task<Loja?> ObterDoCatalogoAsync(Guid id, CancellationToken ct);
    Task<int> ContarCopiasAsync(Guid lojaId, CancellationToken ct);
    Task<List<Secao>> ListarSecoesDoCatalogoAsync(Guid lojaId, CancellationToken ct);
    Task<List<SecaoCategoria>> ListarVinculosDoCatalogoAsync(Guid lojaId, CancellationToken ct);

    /// <summary>Mercados do grupo por nome. Com incluirExcluidas traz também os apagados (para mostrar onde itens antigos foram comprados).</summary>
    Task<List<Loja>> ListarAsync(Guid grupoId, bool incluirExcluidas, CancellationToken ct);

    Task<Loja?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarSecoesAsync(Guid grupoId, CancellationToken ct);
    Task<List<Secao>> ListarSecoesAsync(Guid grupoId, Guid lojaId, CancellationToken ct);
    Task<List<SecaoCategoria>> ListarVinculosAsync(Guid grupoId, Guid lojaId, CancellationToken ct);
    Task<List<LocalDoProduto>> ListarLocaisDeProdutosAsync(Guid grupoId, Guid lojaId, CancellationToken ct);
    Task AdicionarLocaisAsync(IEnumerable<LocalDoProduto> locais, CancellationToken ct);
    void RemoverLocais(IEnumerable<LocalDoProduto> locais);
    Task AdicionarAsync(Loja loja, CancellationToken ct);
    Task AdicionarMapaAsync(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos, CancellationToken ct);
    void RemoverMapa(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos);
}
