using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
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
