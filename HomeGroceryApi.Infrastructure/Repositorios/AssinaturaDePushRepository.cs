using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class AssinaturaDePushRepository : IAssinaturaDePushRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public AssinaturaDePushRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<AssinaturaDePush?> ObterPorEndpointAsync(string endpoint, CancellationToken ct)
    {
        return _contexto.AssinaturasDePush.FirstOrDefaultAsync(a => a.Endpoint == endpoint, ct);
    }

    public async Task AdicionarAsync(AssinaturaDePush assinatura, CancellationToken ct)
    {
        await _contexto.AssinaturasDePush.AddAsync(assinatura, ct);
    }

    public Task<List<AssinaturaDePush>> ListarDosUsuariosAsync(IReadOnlyCollection<Guid> usuarios, CancellationToken ct)
    {
        return _contexto.AssinaturasDePush.AsNoTracking().Where(a => usuarios.Contains(a.UsuarioId)).ToListAsync(ct);
    }

    public Task RemoverPorEndpointAsync(string endpoint, Guid? usuarioId, CancellationToken ct)
    {
        return _contexto.AssinaturasDePush.Where(a => a.Endpoint == endpoint && (usuarioId == null || a.UsuarioId == usuarioId)).ExecuteDeleteAsync(ct);
    }
}
