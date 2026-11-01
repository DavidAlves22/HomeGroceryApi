using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface ITokenDeRenovacaoRepository
{
    Task AdicionarAsync(TokenDeRenovacao token, CancellationToken ct);
    Task<TokenDeRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct);
}
