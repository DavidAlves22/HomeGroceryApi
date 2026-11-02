using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Vínculo de um usuário com um grupo. Um usuário pode ter mais de um vínculo (o grupo pessoal e o
/// da família), mas só um fica ATIVO: é o grupo cujos dados ele vê e altera.
/// </summary>
public sealed class MembroDoGrupo
{
    public Guid GrupoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public PapelNoGrupoEnum Papel { get; private set; }
    public DateTime EntrouEm { get; private set; }
    public bool Ativo { get; private set; }

    private MembroDoGrupo()
    {
    }

    public static MembroDoGrupo Criar(Guid grupoId, Guid usuarioId, PapelNoGrupoEnum papel, DateTime agora, bool ativo = true)
    {
        return new MembroDoGrupo { GrupoId = grupoId, UsuarioId = usuarioId, Papel = papel, EntrouEm = agora, Ativo = ativo };
    }

    public bool Administrador => Papel == PapelNoGrupoEnum.Administrador;

    public void Ativar()
    {
        Ativo = true;
    }

    public void Desativar()
    {
        Ativo = false;
    }

    public void Promover()
    {
        Papel = PapelNoGrupoEnum.Administrador;
    }
}
