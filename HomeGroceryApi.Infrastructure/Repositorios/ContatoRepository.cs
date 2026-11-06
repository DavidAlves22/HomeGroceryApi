using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class ContatoRepository : IContatoRepository
{
    private readonly HomeGroceryDbContext _db;

    public ContatoRepository(HomeGroceryDbContext db)
    {
        _db = db;
    }

    public async Task AdicionarAsync(MensagemDeContato mensagem, CancellationToken ct)
    {
        await _db.MensagensDeContato.AddAsync(mensagem, ct);
    }

    public Task<MensagemDeContato?> ObterAsync(Guid id, CancellationToken ct)
    {
        return _db.MensagensDeContato.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task<List<MensagemDeContato>> ListarDoUsuarioAsync(Guid usuarioId, int limite, CancellationToken ct)
    {
        return _db.MensagensDeContato.AsNoTracking().Where(m => m.UsuarioId == usuarioId).OrderByDescending(m => m.CriadoEm).Take(limite).ToListAsync(ct);
    }

    public Task<List<MensagemDeContato>> ListarTodasAsync(int limite, CancellationToken ct)
    {
        return _db.MensagensDeContato.AsNoTracking().OrderByDescending(m => m.CriadoEm).Take(limite).ToListAsync(ct);
    }

    public Task<int> ContarAguardandoRespostaAsync(CancellationToken ct)
    {
        return _db.MensagensDeContato.CountAsync(m => m.Status == StatusDaMensagemEnum.Recebida || m.Status == StatusDaMensagemEnum.EmAnalise, ct);
    }

    public Task<int> ContarEnviadasDesdeAsync(Guid usuarioId, DateTime desde, CancellationToken ct)
    {
        return _db.MensagensDeContato.CountAsync(m => m.UsuarioId == usuarioId && m.CriadoEm >= desde, ct);
    }

    public Task<List<InformacaoDeContato>> ListarInformacoesAsync(CancellationToken ct)
    {
        return _db.InformacoesDeContato.AsNoTracking().OrderBy(i => i.Ordem).ThenBy(i => i.CriadoEm).ToListAsync(ct);
    }

    public Task<InformacaoDeContato?> ObterInformacaoAsync(Guid id, CancellationToken ct)
    {
        return _db.InformacoesDeContato.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task AdicionarInformacaoAsync(InformacaoDeContato informacao, CancellationToken ct)
    {
        await _db.InformacoesDeContato.AddAsync(informacao, ct);
    }

    public void RemoverInformacao(InformacaoDeContato informacao)
    {
        _db.InformacoesDeContato.Remove(informacao);
    }
}
