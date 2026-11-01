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
}
