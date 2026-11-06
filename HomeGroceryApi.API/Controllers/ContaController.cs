using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Conta;
using Microsoft.AspNetCore.Authorization;
using HomeGroceryApi.CrossCutting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("conta")]
public sealed class ContaController : ControllerBase
{
    /// <summary>Apaga a conta e os dados dela do banco. Pede o e-mail da conta e a senha (se a conta tem senha própria). Usa o limite de tentativas do login.</summary>
    [HttpPost("encerrar")]
    [EnableRateLimiting(ConfiguracoesIniciais.LimiteDeAutenticacao)]
    public async Task<IActionResult> Encerrar([FromBody] EncerrarContaRequisicao requisicao, [FromServices] EncerrarConta casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }

    /// <summary>Troca o nome de quem está logado (aparece para a família).</summary>
    [HttpPut("nome")]
    public async Task<ActionResult<UsuarioResposta>> AlterarNome([FromBody] AlterarNomeRequisicao requisicao, [FromServices] AlterarNomeDoUsuario casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }
}
