namespace HomeGroceryApi.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiraEm) GerarTokenDeAcesso(Guid usuarioId, string email, Guid grupoId);
    (string Token, DateTime ExpiraEm) GerarTokenDeRenovacao();
    string CalcularHash(string token);
}
