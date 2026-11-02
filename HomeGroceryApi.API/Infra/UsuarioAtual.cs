using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Infrastructure.Servicos;

namespace HomeGroceryApi.API.Infra;

/// <summary>
/// Lê usuário e grupo das claims do JWT (já validado e assinado pelo servidor), nunca de algo que o cliente possa forjar.
/// </summary>
public sealed class UsuarioAtual : IUsuarioAtual
{
    private readonly IHttpContextAccessor _acessor;

    public UsuarioAtual(IHttpContextAccessor acessor)
    {
        _acessor = acessor;
    }

    public Guid UsuarioId => LerClaim("sub");

    public Guid GrupoId => LerClaim(TokenService.ClaimDoGrupo);

    private Guid LerClaim(string tipo)
    {
        var valor = _acessor.HttpContext?.User.FindFirst(tipo)?.Value;
        return Guid.TryParse(valor, out var id) ? id : throw new ErroDeAutenticacao("Sessão inválida. Entre novamente.");
    }
}
