using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Grupos;
using HomeGroceryApi.CrossCutting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HomeGroceryApi.API.Controllers;

/// <summary>Grupo familiar: ver membros, convidar, entrar com código, sair e remover.</summary>
[ApiController]
[Authorize]
[Route("grupo")]
public sealed class GrupoController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GrupoResposta>> Obter([FromServices] ObterMeuGrupo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpPut]
    public async Task<IActionResult> Renomear([FromBody] NomeRequisicao requisicao, [FromServices] RenomearGrupo casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }

    [HttpPost("convites")]
    public async Task<ActionResult<ConviteResposta>> CriarConvite([FromServices] CriarConvite casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(ct));
    }

    // Mesmo limite das rotas de login: freia quem tenta adivinhar códigos de convite.
    [HttpPost("entrar")]
    [EnableRateLimiting(ConfiguracoesIniciais.LimiteDeAutenticacao)]
    public async Task<ActionResult<SessaoResposta>> Entrar([FromBody] EntrarNoGrupoRequisicao requisicao, [FromServices] EntrarEmGrupo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPost("sair")]
    public async Task<ActionResult<SessaoResposta>> Sair([FromServices] SairDoGrupo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpDelete("membros/{usuarioId:guid}")]
    public async Task<IActionResult> RemoverMembro(Guid usuarioId, [FromServices] RemoverMembroDoGrupo casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(usuarioId, ct);
        return NoContent();
    }
}
