using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>
/// Health check chamado pelo app ao abrir. Confirma que a API responde e que o banco está acessível.
/// </summary>
[ApiController]
[Route("saude")]
public sealed class SaudeController : ControllerBase
{
    private readonly HomeGroceryDbContext _contexto;

    public SaudeController(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken ct)
    {
        var bancoOk = await _contexto.Database.CanConnectAsync(ct);
        return bancoOk ? Ok(new { status = "ok" }) : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "banco-indisponivel" });
    }
}
