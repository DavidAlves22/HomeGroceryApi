using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface IGrupoRepository
{
    Task AdicionarAsync(Grupo grupo, MembroDoGrupo membro, IEnumerable<Categoria> categorias, CancellationToken ct);
    Task<Guid?> ObterGrupoPessoalDoUsuarioAsync(Guid usuarioId, CancellationToken ct);
}
