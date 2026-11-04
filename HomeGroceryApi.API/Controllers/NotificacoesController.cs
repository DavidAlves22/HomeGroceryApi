using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Notificacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("notificacoes")]
public sealed class NotificacoesController : ControllerBase
{
    /// <summary>Chave pública para assinar os avisos. 404 quando o servidor não tem os avisos ligados.</summary>
    [HttpGet("chave")]
    public ActionResult<ChaveDePushResposta> Chave([FromServices] ObterChaveDePush casoDeUso)
    {
        return Ok(casoDeUso.Executar());
    }

    [HttpPut("assinatura")]
    public async Task<IActionResult> Assinar([FromBody] AssinarPushRequisicao requisicao, [FromServices] AssinarPush casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }

    [HttpPost("assinatura/cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] CancelarPushRequisicao requisicao, [FromServices] CancelarPush casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }
}
