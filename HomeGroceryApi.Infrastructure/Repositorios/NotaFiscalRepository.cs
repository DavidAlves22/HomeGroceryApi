using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class NotaFiscalRepository : INotaFiscalRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public NotaFiscalRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<VinculoDeProdutoDaNota>> ListarVinculosAsync(Guid grupoId, string cnpj, CancellationToken ct)
    {
        return _contexto.VinculosDaNota.Where(v => v.GrupoId == grupoId && v.Cnpj == cnpj).ToListAsync(ct);
    }

    public async Task AdicionarVinculoAsync(VinculoDeProdutoDaNota vinculo, CancellationToken ct)
    {
        await _contexto.VinculosDaNota.AddAsync(vinculo, ct);
    }

    public Task<bool> ExisteNotaAsync(Guid grupoId, string impressao, CancellationToken ct)
    {
        return _contexto.NotasImportadas.AnyAsync(n => n.GrupoId == grupoId && n.Impressao == impressao, ct);
    }

    public Task<bool> ExisteNotaPorChaveAsync(Guid grupoId, string chaveDeAcesso, CancellationToken ct)
    {
        return _contexto.NotasImportadas.AnyAsync(n => n.GrupoId == grupoId && n.ChaveDeAcesso == chaveDeAcesso, ct);
    }

    public async Task AdicionarNotaAsync(NotaImportada nota, CancellationToken ct)
    {
        await _contexto.NotasImportadas.AddAsync(nota, ct);
    }
}
