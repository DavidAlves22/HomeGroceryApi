using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Categorias;
using HomeGroceryApi.Application.UseCases.Modelo;
using HomeGroceryApi.Application.UseCases.Produtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>
/// Controllers finos: recebem a requisição, chamam UM caso de uso e devolvem o resultado.
/// O erro vira HTTP no TratadorDeErros, então não há try/catch aqui.
/// </summary>
[ApiController]
[Authorize]
[Route("categorias")]
public sealed class CategoriasController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoriaResposta>>> Listar([FromServices] ListarCategorias casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaResposta>> Criar([FromBody] NomeRequisicao requisicao, [FromServices] CriarCategoria casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoriaResposta>> Renomear(Guid id, [FromBody] NomeRequisicao requisicao, [FromServices] RenomearCategoria casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, [FromServices] ExcluirCategoria casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    [HttpPut("ordem")]
    public async Task<ActionResult<IReadOnlyList<CategoriaResposta>>> Reordenar([FromBody] ReordenarRequisicao requisicao, [FromServices] ReordenarCategorias casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }
}

[ApiController]
[Authorize]
[Route("produtos")]
public sealed class ProdutosController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProdutoResposta>>> Buscar([FromQuery] string? q, [FromServices] BuscarProdutos casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(q, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProdutoResposta>> Editar(Guid id, [FromBody] EditarProdutoRequisicao requisicao, [FromServices] EditarProduto casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, [FromServices] ExcluirProduto casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("modelo/itens")]
public sealed class ModeloController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ItemDoModeloResposta>>> Listar([FromQuery] string? q, [FromServices] ListarItensDoModelo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(q, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ItemDoModeloResposta>> Adicionar([FromBody] ItemDoModeloRequisicao requisicao, [FromServices] AdicionarItemAoModelo casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ItemDoModeloResposta>> Editar(Guid id, [FromBody] ItemDoModeloRequisicao requisicao, [FromServices] EditarItemDoModelo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, [FromServices] RemoverItemDoModelo casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    [HttpPut("ordem")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarRequisicao requisicao, [FromServices] ReordenarItensDoModelo casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }
}
