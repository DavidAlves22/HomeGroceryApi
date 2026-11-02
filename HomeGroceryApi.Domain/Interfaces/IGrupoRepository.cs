using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface IGrupoRepository
{
    Task AdicionarAsync(Grupo grupo, MembroDoGrupo membro, IEnumerable<Categoria> categorias, CancellationToken ct);
    Task<Grupo?> ObterAsync(Guid grupoId, CancellationToken ct);

    /// <summary>Grupo cujos dados o usuário vê agora (nulo se ele não tem vínculo ativo).</summary>
    Task<Guid?> ObterGrupoAtivoDoUsuarioAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Confere, a cada requisição, se o usuário ainda pertence ao grupo do token (e se ele é o ativo).</summary>
    Task<bool> EhMembroAtivoAsync(Guid usuarioId, Guid grupoId, CancellationToken ct);

    Task<MembroDoGrupo?> ObterMembroAsync(Guid grupoId, Guid usuarioId, CancellationToken ct);
    Task<List<MembroDoGrupo>> ListarMembrosAsync(Guid grupoId, CancellationToken ct);
    Task<List<MembroDoGrupo>> ListarVinculosDoUsuarioAsync(Guid usuarioId, CancellationToken ct);
    Task AdicionarMembroAsync(MembroDoGrupo membro, CancellationToken ct);
    void RemoverMembro(MembroDoGrupo membro);
}

public interface IConviteRepository
{
    Task AdicionarAsync(Convite convite, CancellationToken ct);
    Task<Convite?> ObterPorCodigoAsync(string codigoNormalizado, CancellationToken ct);
}
