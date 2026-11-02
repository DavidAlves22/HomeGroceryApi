using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Listas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("listas")]
public sealed class ListasController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ListaResumoResposta>>> Listar([FromServices] ListarListas casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListaResposta>> Obter(Guid id, [FromServices] ObterLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ListaResposta>> Criar([FromBody] CriarListaRequisicao requisicao, [FromServices] CriarLista casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<ActionResult<ListaResumoResposta>> Finalizar(Guid id, [FromServices] FinalizarLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpPost("{id:guid}/reabrir")]
    public async Task<ActionResult<ListaResumoResposta>> Reabrir(Guid id, [FromServices] ReabrirLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpPost("{id:guid}/itens")]
    public async Task<ActionResult<ItemDaListaResposta>> AdicionarItem(Guid id, [FromBody] ItemDaListaRequisicao requisicao, [FromServices] AdicionarItemNaLista casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpPut("{id:guid}/itens/ordem")]
    public async Task<IActionResult> ReordenarItens(Guid id, [FromBody] ReordenarRequisicao requisicao, [FromServices] ReordenarItensDaLista casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, requisicao, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/itens/{itemId:guid}")]
    public async Task<ActionResult<ItemDaListaResposta>> EditarItem(Guid id, Guid itemId, [FromBody] EditarItemDaListaRequisicao requisicao, [FromServices] EditarItemDaLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, itemId, requisicao, ct));
    }

    [HttpDelete("{id:guid}/itens/{itemId:guid}")]
    public async Task<IActionResult> RemoverItem(Guid id, Guid itemId, [FromServices] RemoverItemDaLista casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, itemId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/itens/{itemId:guid}/comprado")]
    public async Task<ActionResult<ItemDaListaResposta>> MarcarComprado(Guid id, Guid itemId, [FromBody] MarcarCompradoRequisicao requisicao, [FromServices] MarcarItemComprado casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, itemId, requisicao, ct));
    }
}
