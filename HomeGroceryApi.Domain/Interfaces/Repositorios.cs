using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Domain.Interfaces;

public sealed record ResumoDeLista(Guid Id, string Competencia, string Nome, bool Principal, StatusDaListaEnum Status, DateTime? FinalizadaEm, int TotalDeItens, int Comprados, decimal TotalEstimado, decimal TotalRealizado);

public interface ICategoriaRepository
{
    /// <summary>Categorias do grupo em ordem. Com incluirExcluidas traz também as apagadas (para exibir nomes do histórico).</summary>
    Task<List<Categoria>> ListarAsync(Guid grupoId, bool incluirExcluidas, CancellationToken ct);
    Task<Categoria?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task AdicionarAsync(Categoria categoria, CancellationToken ct);
}

public interface IProdutoRepository
{
    Task<List<Produto>> BuscarAsync(Guid grupoId, string? termo, CancellationToken ct);
    Task<Produto?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task<Produto?> ObterPorNomeAsync(Guid grupoId, string nome, CancellationToken ct);
    Task AdicionarAsync(Produto produto, CancellationToken ct);
}

public interface IModeloRepository
{
    Task<List<ItemDoModelo>> ListarAsync(Guid grupoId, string? termo, CancellationToken ct);
    Task<ItemDoModelo?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task<int> ProximaOrdemAsync(Guid grupoId, CancellationToken ct);
    Task AdicionarAsync(ItemDoModelo item, CancellationToken ct);
    void Remover(ItemDoModelo item);
}

public interface IListaRepository
{
    Task<List<ListaDeCompras>> ListarAsync(Guid grupoId, CancellationToken ct);

    /// <summary>Todos os itens de todas as listas do grupo, com o mês de cada um (base de resumos e análises).</summary>
    Task<List<ItemComCompetencia>> ListarItensComCompetenciaAsync(Guid grupoId, CancellationToken ct);

    Task<ListaDeCompras?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct);
    Task<bool> ExistePrincipalAsync(Guid grupoId, string competencia, CancellationToken ct);
    Task AdicionarAsync(ListaDeCompras lista, IEnumerable<ItemDaLista> itens, CancellationToken ct);
    Task<List<ItemDaLista>> ListarItensAsync(Guid grupoId, Guid listaId, CancellationToken ct);
    Task<ItemDaLista?> ObterItemAsync(Guid grupoId, Guid listaId, Guid itemId, CancellationToken ct);
    Task<int> ProximaOrdemDeItemAsync(Guid listaId, CancellationToken ct);
    Task AdicionarItemAsync(ItemDaLista item, CancellationToken ct);
    void RemoverItem(ItemDaLista item);
}
