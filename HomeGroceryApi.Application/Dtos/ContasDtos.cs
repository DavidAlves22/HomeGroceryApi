namespace HomeGroceryApi.Application.Dtos;

public sealed record ContaDoUsuario(Guid Id, string Nome, string Email, bool Bloqueada = false);

public sealed record ResultadoDeCriacaoDeConta(Guid? UsuarioId, bool EmailJaCadastrado, IReadOnlyList<string> Erros)
{
    public bool Sucesso => UsuarioId.HasValue;
}
