using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Opcoes;
using Microsoft.Extensions.Options;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>Administrador = e-mail listado em Admin:Emails. Sem a lista ninguém é administrador (painel fechado).</summary>
public sealed class Administradores : IAdministradores
{
    private readonly HashSet<string> _emails;

    public Administradores(IOptions<AdminOpcoes> opcoes)
    {
        _emails = opcoes.Value.Emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim().ToLowerInvariant()).ToHashSet();
    }

    public bool EhAdministrador(string email) => !string.IsNullOrWhiteSpace(email) && _emails.Contains(email.Trim().ToLowerInvariant());
}
