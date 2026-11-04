using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface ITokenDeRenovacaoRepository
{
    Task AdicionarAsync(TokenDeRenovacao token, CancellationToken ct);
    Task<TokenDeRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct);

    /// <summary>Encerra todas as sessões do usuário (troca de senha): ninguém continua logado com a senha antiga.</summary>
    Task RevogarTodosDoUsuarioAsync(Guid usuarioId, DateTime agora, CancellationToken ct);
}
