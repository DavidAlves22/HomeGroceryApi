using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Admin;
using HomeGroceryApi.Infrastructure.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>
/// Painel de administração. O papel Admin é conferido no servidor a cada chamada (claim assinada no JWT): quem não é
/// administrador recebe 403 mesmo conhecendo a URL.
/// </summary>
[ApiController]
[Authorize(Roles = TokenService.PapelAdmin)]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("resumo")]
    public async Task<ActionResult<AdminResumoResposta>> Resumo([FromServices] ConsultarResumoDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("series")]
    public async Task<ActionResult<AdminSeriesResposta>> Series([FromQuery] int dias, [FromServices] ConsultarSeriesDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(dias, ct));
    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<AdminUsuariosResposta>> Usuarios([FromQuery] int pagina, [FromServices] ListarUsuariosDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(pagina, ct));
    }
}
