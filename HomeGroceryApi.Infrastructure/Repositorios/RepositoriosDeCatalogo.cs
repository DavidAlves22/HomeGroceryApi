using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

internal static class Busca
{
    /// <summary>Monta o padrão do ILike escapando % _ \ que o usuário possa ter digitado.</summary>
    public static string Contem(string termo)
    {
        var escapado = termo.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escapado}%";
    }
}

public sealed class CategoriaRepository : ICategoriaRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public CategoriaRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<Categoria>> ListarAsync(Guid grupoId, bool incluirExcluidas, CancellationToken ct)
    {
        return _contexto.Categorias.Where(c => c.GrupoId == grupoId && (incluirExcluidas || c.ExcluidoEm == null)).OrderBy(c => c.Ordem).ToListAsync(ct);
    }

    public Task<Categoria?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct)
    {
        return _contexto.Categorias.FirstOrDefaultAsync(c => c.GrupoId == grupoId && c.Id == id, ct);
    }

    public async Task AdicionarAsync(Categoria categoria, CancellationToken ct)
    {
        await _contexto.Categorias.AddAsync(categoria, ct);
    }
}

public sealed class ProdutoRepository : IProdutoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public ProdutoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<Produto>> BuscarAsync(Guid grupoId, string? termo, CancellationToken ct)
    {
        var consulta = _contexto.Produtos.Where(p => p.GrupoId == grupoId && p.ExcluidoEm == null);
        consulta = consulta.OrderBy(p => p.Nome);

        // Com termo (sugestões ao digitar) limita a 50; sem termo devolve todos, porque outros casos de uso precisam de todos.
        if (string.IsNullOrWhiteSpace(termo)) return consulta.ToListAsync(ct);
        return consulta.Where(p => EF.Functions.ILike(p.Nome, Busca.Contem(termo))).Take(50).ToListAsync(ct);
    }

    public Task<Produto?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct)
    {
        return _contexto.Produtos.FirstOrDefaultAsync(p => p.GrupoId == grupoId && p.Id == id, ct);
    }

    public Task<Produto?> ObterPorNomeAsync(Guid grupoId, string nome, CancellationToken ct)
    {
        var minusculo = nome.Trim().ToLower();
        return _contexto.Produtos.FirstOrDefaultAsync(p => p.GrupoId == grupoId && p.ExcluidoEm == null && p.Nome.ToLower() == minusculo, ct);
    }

    public async Task AdicionarAsync(Produto produto, CancellationToken ct)
    {
        await _contexto.Produtos.AddAsync(produto, ct);
    }
}

public sealed class ModeloRepository : IModeloRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public ModeloRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<ItemDoModelo>> ListarAsync(Guid grupoId, string? termo, CancellationToken ct)
    {
        var consulta = _contexto.ItensDoModelo.Where(i => i.GrupoId == grupoId);
        if (!string.IsNullOrWhiteSpace(termo)) consulta = consulta.Where(i => EF.Functions.ILike(i.Nome, Busca.Contem(termo)));
        return consulta.OrderBy(i => i.Ordem).ToListAsync(ct);
    }

    public Task<ItemDoModelo?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct)
    {
        return _contexto.ItensDoModelo.FirstOrDefaultAsync(i => i.GrupoId == grupoId && i.Id == id, ct);
    }

    public async Task<int> ProximaOrdemAsync(Guid grupoId, CancellationToken ct)
    {
        var maior = await _contexto.ItensDoModelo.Where(i => i.GrupoId == grupoId).MaxAsync(i => (int?)i.Ordem, ct);
        return (maior ?? -1) + 1;
    }

    public async Task AdicionarAsync(ItemDoModelo item, CancellationToken ct)
    {
        await _contexto.ItensDoModelo.AddAsync(item, ct);
    }

    public void Remover(ItemDoModelo item)
    {
        _contexto.ItensDoModelo.Remove(item);
    }
}

public sealed class ListaRepository : IListaRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public ListaRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<ListaDeCompras>> ListarAsync(Guid grupoId, CancellationToken ct)
    {
        return _contexto.Listas.AsNoTracking().Where(l => l.GrupoId == grupoId).ToListAsync(ct);
    }

    public async Task<List<ItemComCompetencia>> ListarItensComCompetenciaAsync(Guid grupoId, CancellationToken ct)
    {
        var linhas = await (
            from item in _contexto.ItensDasListas.AsNoTracking()
            join lista in _contexto.Listas.AsNoTracking() on item.ListaId equals lista.Id
            where lista.GrupoId == grupoId && item.GrupoId == grupoId
            select new { lista.Competencia, Item = item }).ToListAsync(ct);

        return linhas.Select(l => new ItemComCompetencia(l.Competencia, l.Item)).ToList();
    }

    public Task<ListaDeCompras?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct)
    {
        return _contexto.Listas.FirstOrDefaultAsync(l => l.GrupoId == grupoId && l.Id == id, ct);
    }

    public Task<bool> ExistePrincipalAsync(Guid grupoId, string competencia, CancellationToken ct)
    {
        return _contexto.Listas.AnyAsync(l => l.GrupoId == grupoId && l.Competencia == competencia && l.Principal, ct);
    }

    public async Task AdicionarAsync(ListaDeCompras lista, IEnumerable<ItemDaLista> itens, CancellationToken ct)
    {
        await _contexto.Listas.AddAsync(lista, ct);
        await _contexto.ItensDasListas.AddRangeAsync(itens, ct);
    }

    public Task<List<ItemDaLista>> ListarItensAsync(Guid grupoId, Guid listaId, CancellationToken ct)
    {
        return _contexto.ItensDasListas.Where(i => i.GrupoId == grupoId && i.ListaId == listaId).OrderBy(i => i.Ordem).ToListAsync(ct);
    }

    public Task<ItemDaLista?> ObterItemAsync(Guid grupoId, Guid listaId, Guid itemId, CancellationToken ct)
    {
        return _contexto.ItensDasListas.FirstOrDefaultAsync(i => i.GrupoId == grupoId && i.ListaId == listaId && i.Id == itemId, ct);
    }

    public async Task<int> ProximaOrdemDeItemAsync(Guid listaId, CancellationToken ct)
    {
        var maior = await _contexto.ItensDasListas.Where(i => i.ListaId == listaId).MaxAsync(i => (int?)i.Ordem, ct);
        return (maior ?? -1) + 1;
    }

    public async Task AdicionarItemAsync(ItemDaLista item, CancellationToken ct)
    {
        await _contexto.ItensDasListas.AddAsync(item, ct);
    }

    public void RemoverItem(ItemDaLista item)
    {
        _contexto.ItensDasListas.Remove(item);
    }

    public void RemoverLista(ListaDeCompras lista, IEnumerable<ItemDaLista> itens)
    {
        _contexto.ItensDasListas.RemoveRange(itens);
        _contexto.Listas.Remove(lista);
    }
}
