using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Listas;
using HomeGroceryApi.Application.UseCases.Notas;
using HomeGroceryApi.Application.UseCases.Rotas;
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

    /// <summary>Produtos que, pelo ritmo de compra de cada um, devem entrar na lista do mês.</summary>
    [HttpGet("sugestao")]
    public async Task<ActionResult<SugestaoDeListaResposta>> Sugestao([FromQuery] string competencia, [FromServices] ObterSugestaoDeLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(competencia, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ListaResposta>> Criar([FromBody] CriarListaRequisicao requisicao, [FromServices] CriarLista casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    /// <summary>Cancela a compra em andamento e apaga a lista (não vai para o histórico).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancelar(Guid id, [FromServices] CancelarLista casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    /// <summary>Define quem pega cada categoria nesta ida ao mercado (substitui a divisão atual).</summary>
    [HttpPut("{id:guid}/divisao")]
    public async Task<ActionResult<IReadOnlyList<DivisaoDaCategoriaResposta>>> Dividir(Guid id, [FromBody] DividirCompraRequisicao requisicao, [FromServices] DividirCompra casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    /// <summary>Importa uma nota fiscal já lida pelo app: preços e quantidades pagos, itens marcados como comprados.</summary>
    [HttpPost("{id:guid}/nota")]
    public async Task<ActionResult<ListaResposta>> ImportarNota(Guid id, [FromBody] ImportarNotaRequisicao requisicao, [FromServices] ImportarNotaNaLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
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

    [HttpPut("{id:guid}/itens/{itemId:guid}/local")]
    public async Task<ActionResult<IReadOnlyList<LocalDoItemResposta>>> DefinirLocal(Guid id, Guid itemId, [FromBody] DefinirLocalDoItemRequisicao requisicao, [FromServices] DefinirLocalDoItem casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, itemId, requisicao, ct));
    }

    [HttpPut("{id:guid}/itens/{itemId:guid}/sem-na-loja")]
    public async Task<ActionResult<ItemDaListaResposta>> MarcarSemNaLoja(Guid id, Guid itemId, [FromBody] SemNaLojaRequisicao requisicao, [FromServices] MarcarItemSemNaLoja casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, itemId, requisicao, ct));
    }

    /// <summary>Rota dentro de um mercado, só com os itens que ainda faltam. É POST porque leva a posição atual no corpo.</summary>
    [HttpPost("{id:guid}/rota")]
    public async Task<ActionResult<RotaResposta>> PlanejarRota(Guid id, [FromBody] RotaRequisicao requisicao, [FromServices] PlanejarRotaDaLista casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }
}
