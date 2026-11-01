using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class GrupoRepository : IGrupoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public GrupoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(Grupo grupo, MembroDoGrupo membro, IEnumerable<Categoria> categorias, CancellationToken ct)
    {
        await _contexto.Grupos.AddAsync(grupo, ct);
        await _contexto.Membros.AddAsync(membro, ct);
        await _contexto.Categorias.AddRangeAsync(categorias, ct);
    }

    public async Task<Guid?> ObterGrupoPessoalDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var ids = await _contexto.Membros
            .Where(m => m.UsuarioId == usuarioId)
            .Join(_contexto.Grupos.Where(g => g.Pessoal), m => m.GrupoId, g => g.Id, (m, g) => g.Id)
            .ToListAsync(ct);

        return ids.Count == 0 ? null : ids[0];
    }
}

public sealed class TokenDeRenovacaoRepository : ITokenDeRenovacaoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public TokenDeRenovacaoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(TokenDeRenovacao token, CancellationToken ct)
    {
        await _contexto.TokensDeRenovacao.AddAsync(token, ct);
    }

    public Task<TokenDeRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct)
    {
        return _contexto.TokensDeRenovacao.FirstOrDefaultAsync(t => t.Hash == hash, ct);
    }
}

public sealed class UnidadeDeTrabalho : IUnidadeDeTrabalho
{
    private readonly HomeGroceryDbContext _contexto;

    public UnidadeDeTrabalho(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task SalvarAsync(CancellationToken ct)
    {
        return _contexto.SaveChangesAsync(ct);
    }

    public async Task ExecutarEmTransacaoAsync(Func<Task> acao, CancellationToken ct)
    {
        await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);
        await acao();
        await transacao.CommitAsync(ct);
    }
}
