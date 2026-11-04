using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Lojas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>Catálogo de mapas de mercado compartilhados por todos os grupos (só leitura e cópia).</summary>
[ApiController]
[Authorize]
[Route("catalogo/mercados")]
public sealed class CatalogoController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogoMercadoResposta>>> Listar([FromQuery] string? busca, [FromQuery] string? cidade, [FromServices] ListarCatalogoDeMercados casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(busca, cidade, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CatalogoMercadoDetalheResposta>> Obter(Guid id, [FromServices] ObterMercadoDoCatalogo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpPost("{id:guid}/copiar")]
    public async Task<ActionResult<LojaResposta>> Copiar(Guid id, [FromServices] CopiarMercadoDoCatalogo casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(id, ct));
    }
}
