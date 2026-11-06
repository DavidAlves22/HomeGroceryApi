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

    /// <summary>O que repor agora pelo ritmo de compra e quais itens da lista padrão estão parados.</summary>
    [HttpGet("avisos")]
    public async Task<ActionResult<AvisosDeRitmoResposta>> Avisos([FromServices] ObterAvisosDeRitmo casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    /// <summary>Preços abaixo do habitual, produtos de época e produtos que costumam ir juntos.</summary>
    [HttpGet("padroes")]
    public async Task<ActionResult<PadroesDeCompraResposta>> Padroes([FromServices] ObterPadroesDeCompra casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    /// <summary>Em qual mercado cada produto costuma ser comprado e quem costuma comprar cada categoria.</summary>
    [HttpGet("habitos")]
    public async Task<ActionResult<HabitosDeCompraResposta>> Habitos([FromServices] ObterHabitosDeCompra casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("precos")]
    public async Task<ActionResult<AnaliseDePrecosResposta>> Precos([FromServices] ObterAnaliseDePrecos casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }
}
