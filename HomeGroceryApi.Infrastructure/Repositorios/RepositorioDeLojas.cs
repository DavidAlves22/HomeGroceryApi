using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class LojaRepository : ILojaRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public LojaRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<ItemDoCatalogo>> ListarCatalogoAsync(Guid grupoIdDoUsuario, string? busca, string? cidade, int limite, CancellationToken ct)
    {
        var consulta = _contexto.Lojas.AsNoTracking().Where(l => l.Compartilhada && l.ExcluidoEm == null && l.GrupoId != grupoIdDoUsuario && l.Cidade != null && (l.OrigemId == null || l.Versao > 1) && _contexto.Secoes.Any(s => s.LojaId == l.Id));
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var padrao = "%" + busca.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(l => EF.Functions.ILike(l.Nome, padrao, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(cidade))
        {
            var cidadeBusca = AnaliseDeCompras.Normalizar(cidade);
            consulta = consulta.Where(l => l.CidadeBusca!.Contains(cidadeBusca));
        }

        var linhas = await consulta
            .Select(l => new { Loja = l, Secoes = _contexto.Secoes.Count(s => s.LojaId == l.Id), Copias = _contexto.Lojas.Count(c => c.OrigemId == l.Id && c.ExcluidoEm == null) })
            .OrderByDescending(x => x.Copias).ThenBy(x => x.Loja.Nome)
            .Take(limite)
            .ToListAsync(ct);
        return linhas.Select(x => new ItemDoCatalogo(x.Loja, x.Secoes, x.Copias)).ToList();
    }

    public Task<Loja?> ObterDoCatalogoAsync(Guid id, CancellationToken ct)
    {
        return _contexto.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.Compartilhada && l.ExcluidoEm == null, ct);
    }

    public Task<int> ContarCopiasAsync(Guid lojaId, CancellationToken ct)
    {
        return _contexto.Lojas.CountAsync(l => l.OrigemId == lojaId && l.ExcluidoEm == null, ct);
    }

    public Task<List<Secao>> ListarSecoesDoCatalogoAsync(Guid lojaId, CancellationToken ct)
    {
        return _contexto.Secoes.AsNoTracking().Where(s => s.LojaId == lojaId).OrderBy(s => s.Id).ToListAsync(ct);
    }

    public Task<List<SecaoCategoria>> ListarVinculosDoCatalogoAsync(Guid lojaId, CancellationToken ct)
    {
        return _contexto.SecaoCategorias.AsNoTracking().Where(v => v.LojaId == lojaId).OrderBy(v => v.Id).ToListAsync(ct);
    }

    public Task<List<Loja>> ListarAsync(Guid grupoId, bool incluirExcluidas, CancellationToken ct)
    {
        return _contexto.Lojas.Where(l => l.GrupoId == grupoId && (incluirExcluidas || l.ExcluidoEm == null)).OrderBy(l => l.Nome).ToListAsync(ct);
    }

    public Task<Loja?> ObterAsync(Guid grupoId, Guid id, CancellationToken ct)
    {
        return _contexto.Lojas.FirstOrDefaultAsync(l => l.GrupoId == grupoId && l.Id == id, ct);
    }

    public Task<Dictionary<Guid, int>> ContarSecoesAsync(Guid grupoId, CancellationToken ct)
    {
        return _contexto.Secoes.AsNoTracking().Where(s => s.GrupoId == grupoId).GroupBy(s => s.LojaId).Select(g => new { Loja = g.Key, Total = g.Count() }).ToDictionaryAsync(g => g.Loja, g => g.Total, ct);
    }

    public Task<List<Secao>> ListarSecoesAsync(Guid grupoId, Guid lojaId, CancellationToken ct)
    {
        return _contexto.Secoes.Where(s => s.GrupoId == grupoId && s.LojaId == lojaId).OrderBy(s => s.Id).ToListAsync(ct);
    }

    public Task<List<SecaoCategoria>> ListarVinculosAsync(Guid grupoId, Guid lojaId, CancellationToken ct)
    {
        return _contexto.SecaoCategorias.Where(v => v.GrupoId == grupoId && v.LojaId == lojaId).OrderBy(v => v.Id).ToListAsync(ct);
    }

    public Task<List<LocalDoProduto>> ListarLocaisDeProdutosAsync(Guid grupoId, Guid lojaId, CancellationToken ct)
    {
        return _contexto.LocaisDoProduto.Where(l => l.GrupoId == grupoId && l.LojaId == lojaId).OrderBy(l => l.Id).ToListAsync(ct);
    }

    public async Task AdicionarLocaisAsync(IEnumerable<LocalDoProduto> locais, CancellationToken ct)
    {
        await _contexto.LocaisDoProduto.AddRangeAsync(locais, ct);
    }

    public void RemoverLocais(IEnumerable<LocalDoProduto> locais)
    {
        _contexto.LocaisDoProduto.RemoveRange(locais);
    }

    public async Task AdicionarAsync(Loja loja, CancellationToken ct)
    {
        await _contexto.Lojas.AddAsync(loja, ct);
    }

    public async Task AdicionarMapaAsync(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos, CancellationToken ct)
    {
        await _contexto.Secoes.AddRangeAsync(secoes, ct);
        await _contexto.SecaoCategorias.AddRangeAsync(vinculos, ct);
    }

    public void RemoverMapa(IEnumerable<Secao> secoes, IEnumerable<SecaoCategoria> vinculos)
    {
        _contexto.SecaoCategorias.RemoveRange(vinculos);
        _contexto.Secoes.RemoveRange(secoes);
    }
}
