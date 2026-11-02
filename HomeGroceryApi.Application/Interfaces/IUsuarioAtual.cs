namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// Quem está fazendo a requisição e a qual grupo ela pertence (lidos do token JWT assinado).
/// Todo caso de uso filtra os dados por GrupoId, e é isso que impede ver dados de outro grupo.
/// </summary>
public interface IUsuarioAtual
{
    Guid UsuarioId { get; }
    Guid GrupoId { get; }
}
