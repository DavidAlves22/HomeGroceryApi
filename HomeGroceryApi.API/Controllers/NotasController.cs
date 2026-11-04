using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Notas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("notas")]
public sealed class NotasController : ControllerBase
{
    /// <summary>A nota (pela chave de acesso do QR) já foi importada neste grupo?</summary>
    [HttpGet("importada")]
    public async Task<ActionResult<NotaImportadaResposta>> Importada([FromQuery] string? chave, [FromServices] ConsultarNotaImportada casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(chave, ct));
    }

    /// <summary>Pares "código do mercado = produto" já ensinados para o CNPJ da nota.</summary>
    [HttpGet("vinculos")]
    public async Task<ActionResult<IReadOnlyList<VinculoDaNotaResposta>>> Vinculos([FromQuery] string? cnpj, [FromServices] ObterVinculosDaNota casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(cnpj, ct));
    }
}
