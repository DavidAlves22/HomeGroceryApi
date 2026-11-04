using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Lojas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>Mercados do grupo e o mapa (layout) de cada um.</summary>
[ApiController]
[Authorize]
[Route("lojas")]
public sealed class LojasController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LojaResumoResposta>>> Listar([FromServices] ListarLojas casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LojaResposta>> Obter(Guid id, [FromServices] ObterLoja casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<LojaResposta>> Criar([FromBody] CriarLojaRequisicao requisicao, [FromServices] CriarLoja casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPut("{id:guid}/mapa")]
    public async Task<ActionResult<LojaResposta>> SalvarMapa(Guid id, [FromBody] SalvarMapaRequisicao requisicao, [FromServices] SalvarMapaDaLoja casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, [FromServices] ExcluirLoja casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }
}
