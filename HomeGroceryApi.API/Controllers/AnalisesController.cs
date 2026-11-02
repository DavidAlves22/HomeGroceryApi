using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Analises;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("analises")]
public sealed class AnalisesController : ControllerBase
{
    [HttpGet("gastos")]
    public async Task<ActionResult<AnaliseDeGastosResposta>> Gastos([FromQuery] int meses, [FromQuery] string? competencia, [FromServices] ObterAnaliseDeGastos casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(meses <= 0 ? 6 : meses, competencia, ct));
    }

    [HttpGet("precos")]
    public async Task<ActionResult<AnaliseDePrecosResposta>> Precos([FromServices] ObterAnaliseDePrecos casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }
}
