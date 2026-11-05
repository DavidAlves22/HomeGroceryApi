using HomeGroceryApi.Application.Dtos;

namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// Porta para a gestão de contas. Quem implementa (ASP.NET Identity) fica na Infrastructure,
/// assim os casos de uso não conhecem o framework de identidade.
/// </summary>
public interface IServicoDeContas
{
    Task<ResultadoDeCriacaoDeConta> CriarAsync(string email, string nome, string senha, CancellationToken ct);
    Task<ContaDoUsuario?> ValidarCredenciaisAsync(string email, string senha, CancellationToken ct);
    Task<ContaDoUsuario?> ObterPorIdAsync(Guid usuarioId, CancellationToken ct);
    Task<ContaDoUsuario?> ObterPorEmailAsync(string email, CancellationToken ct);

    /// <summary>Troca a senha sem pedir a antiga (quem chama já provou ser o dono, pelo código do e-mail). Devolve os erros de regra de senha; vazio = trocou.</summary>
    Task<IReadOnlyList<string>> RedefinirSenhaAsync(Guid usuarioId, string novaSenha, CancellationToken ct);

    Task AlterarNomeAsync(Guid usuarioId, string nome, CancellationToken ct);

    /// <summary>Marca o último acesso da conta (login, cadastro ou renovação de sessão).</summary>
    Task RegistrarAcessoAsync(Guid usuarioId, DateTime agora, CancellationToken ct);

    /// <summary>Bloqueia (com a data) ou libera (null) a conta. Devolve false se a conta não existe.</summary>
    Task<bool> DefinirBloqueioAsync(Guid usuarioId, DateTime? bloqueadoEm, CancellationToken ct);

    /// <summary>Conta inexistente também conta como bloqueada: quem não existe mais não pode seguir usando um token antigo.</summary>
    Task<bool> EstaBloqueadaAsync(Guid usuarioId, CancellationToken ct);
}
