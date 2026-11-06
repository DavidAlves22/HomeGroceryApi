using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>Contato com o administrador do app: e-mail configurado por ele e as mensagens (feedback/solicitação) de quem está logado.</summary>
[ApiController]
[Authorize]
[Route("contato")]
public sealed class ContatoController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ContatoResposta>> Obter([FromServices] ObterContato casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("mensagens")]
    public async Task<ActionResult<MensagensResposta>> Listar([FromServices] ListarMinhasMensagens casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpPost("mensagens")]
    public async Task<ActionResult<MensagemResposta>> Enviar([FromBody] EnviarMensagemRequisicao requisicao, [FromServices] EnviarMensagemDeContato casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPut("mensagens/{id:guid}/vista")]
    public async Task<IActionResult> MarcarComoVista(Guid id, [FromServices] MarcarRespostaComoVista casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }
}
