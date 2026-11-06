namespace HomeGroceryApi.Application.Dtos;

/// <param name="SemSenhaPropria">Conta criada pelo Google e que nunca definiu senha: a senha dela é aleatória e ninguém a conhece.</param>
public sealed record ContaDoUsuario(Guid Id, string Nome, string Email, bool Bloqueada = false, bool SemSenhaPropria = false);

/// <summary>Encerrar a conta exige digitar o e-mail da conta e, se a conta tem senha própria, a senha.</summary>
public sealed record EncerrarContaRequisicao(string? Senha, string? ConfirmacaoEmail);

public sealed record ResultadoDeCriacaoDeConta(Guid? UsuarioId, bool EmailJaCadastrado, IReadOnlyList<string> Erros)
{
    public bool Sucesso => UsuarioId.HasValue;
}
