namespace HomeGroceryApi.Application.Interfaces;

/// <summary>Responde se um e-mail está na lista de administradores da configuração.</summary>
public interface IAdministradores
{
    bool EhAdministrador(string email);
}
