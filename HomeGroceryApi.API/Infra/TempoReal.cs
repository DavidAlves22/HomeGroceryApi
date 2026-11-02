using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HomeGroceryApi.API.Infra;

/// <summary>
/// Canal em tempo real do grupo. Cada conexão entra no "quarto" do grupo do token e num quarto só dela
/// (para avisos pessoais, como "você foi removido do grupo"). O hub não recebe nada do cliente: só empurra eventos.
/// </summary>
[Authorize]
public sealed class GrupoHub : Hub
{
    private readonly IGrupoRepository _grupos;

    public GrupoHub(IGrupoRepository grupos)
    {
        _grupos = grupos;
    }

    public static string QuartoDoGrupo(Guid grupoId) => $"grupo:{grupoId}";

    public static string QuartoDoUsuario(Guid usuarioId) => $"usuario:{usuarioId}";

    public override async Task OnConnectedAsync()
    {
        var usuario = Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var u) ? u : Guid.Empty;
        var grupo = Guid.TryParse(Context.User?.FindFirst(TokenService.ClaimDoGrupo)?.Value, out var g) ? g : Guid.Empty;

        // Mesma regra das rotas HTTP: só entra no quarto do grupo quem ainda é membro ativo dele.
        if (usuario == Guid.Empty || grupo == Guid.Empty || !await _grupos.EhMembroAtivoAsync(usuario, grupo, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, QuartoDoGrupo(grupo));
        await Groups.AddToGroupAsync(Context.ConnectionId, QuartoDoUsuario(usuario));
        await base.OnConnectedAsync();
    }
}

public sealed class NotificadorSignalR : INotificador
{
    private readonly IHubContext<GrupoHub> _hub;
    private readonly IHttpContextAccessor _acessor;

    public NotificadorSignalR(IHubContext<GrupoHub> hub, IHttpContextAccessor acessor)
    {
        _hub = hub;
        _acessor = acessor;
    }

    public Task PublicarNoGrupoAsync(Guid grupoId, EventoDeAlteracao evento, CancellationToken ct)
    {
        return _hub.Clients.Group(GrupoHub.QuartoDoGrupo(grupoId)).SendAsync("evento", ComAutor(evento), ct);
    }

    public Task PublicarParaUsuarioAsync(Guid usuarioId, EventoDeAlteracao evento, CancellationToken ct)
    {
        return _hub.Clients.Group(GrupoHub.QuartoDoUsuario(usuarioId)).SendAsync("evento", ComAutor(evento), ct);
    }

    /// <summary>Quem fez a alteração vai no evento: o aparelho dele ignora o próprio aviso (já mostrou a mudança).</summary>
    private EventoDeAlteracao ComAutor(EventoDeAlteracao evento)
    {
        var autor = Guid.TryParse(_acessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : (Guid?)null;
        return evento with { PorUsuarioId = autor };
    }
}

/// <summary>
/// Confere, a cada requisição autenticada, se o usuário ainda é membro ATIVO do grupo escrito no token.
/// Sem isto, quem foi removido (ou saiu) continuaria acessando os dados da família até o token expirar (30 min).
/// A resposta 401 faz o app renovar a sessão, e a renovação já devolve o grupo certo.
/// </summary>
public sealed class ValidarMembroDoGrupoMiddleware
{
    private readonly RequestDelegate _proximo;

    public ValidarMembroDoGrupoMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto, IGrupoRepository grupos)
    {
        // Login e renovação precisam passar: é por eles que alguém removido do grupo recebe o grupo certo.
        if (contexto.Request.Path.StartsWithSegments("/autenticacao"))
        {
            await _proximo(contexto);
            return;
        }

        var usuario = contexto.User.FindFirst("sub")?.Value;
        var grupo = contexto.User.FindFirst(TokenService.ClaimDoGrupo)?.Value;

        if (contexto.User.Identity?.IsAuthenticated == true && Guid.TryParse(usuario, out var usuarioId) && Guid.TryParse(grupo, out var grupoId)
            && !await grupos.EhMembroAtivoAsync(usuarioId, grupoId, contexto.RequestAborted))
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsJsonAsync(new { title = "Não autenticado", status = 401, detail = "Seu acesso a este grupo mudou. Entre novamente." }, contexto.RequestAborted);
            return;
        }

        await _proximo(contexto);
    }
}
