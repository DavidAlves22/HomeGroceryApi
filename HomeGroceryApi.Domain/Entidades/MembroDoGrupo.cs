using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

public sealed class MembroDoGrupo
{
    public Guid GrupoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public PapelNoGrupoEnum Papel { get; private set; }
    public DateTime EntrouEm { get; private set; }

    private MembroDoGrupo()
    {
    }

    public static MembroDoGrupo Criar(Guid grupoId, Guid usuarioId, PapelNoGrupoEnum papel, DateTime agora)
    {
        return new MembroDoGrupo { GrupoId = grupoId, UsuarioId = usuarioId, Papel = papel, EntrouEm = agora };
    }
}
