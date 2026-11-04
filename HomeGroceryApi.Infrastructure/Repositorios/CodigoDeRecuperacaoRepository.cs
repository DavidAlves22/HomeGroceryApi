using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class CodigoDeRecuperacaoRepository : ICodigoDeRecuperacaoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public CodigoDeRecuperacaoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(CodigoDeRecuperacao codigo, CancellationToken ct)
    {
        await _contexto.CodigosDeRecuperacao.AddAsync(codigo, ct);
    }

    public Task<CodigoDeRecuperacao?> ObterAtivoMaisRecenteAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        return _contexto.CodigosDeRecuperacao
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null && c.ExpiraEm > agora && c.Tentativas < CodigoDeRecuperacao.MaximoDeTentativas)
            .OrderByDescending(c => c.CriadoEm)
            .FirstOrDefaultAsync(ct);
    }

    public Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desde, CancellationToken ct)
    {
        return _contexto.CodigosDeRecuperacao.CountAsync(c => c.UsuarioId == usuarioId && c.CriadoEm >= desde, ct);
    }

    public async Task InvalidarAtivosAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        var ativos = await _contexto.CodigosDeRecuperacao.Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null).ToListAsync(ct);
        foreach (var codigo in ativos) codigo.MarcarComoUsado(agora);
    }
}
