using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Autenticacao;
using HomeGroceryApi.CrossCutting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HomeGroceryApi.API.Controllers;

/// <summary>
/// Controller fino: só recebe a requisição e chama o caso de uso. Os erros viram HTTP no TratadorDeErros.
/// </summary>
[ApiController]
[Route("autenticacao")]
[EnableRateLimiting(ConfiguracoesIniciais.LimiteDeAutenticacao)]
public sealed class AutenticacaoController : ControllerBase
{
    [HttpPost("cadastro")]
    public async Task<ActionResult<SessaoResposta>> Cadastrar([FromBody] CadastroRequisicao requisicao, [FromServices] CadastrarUsuario casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPost("login")]
    public async Task<ActionResult<SessaoResposta>> Entrar([FromBody] LoginRequisicao requisicao, [FromServices] FazerLogin casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPost("renovar")]
    public async Task<ActionResult<SessaoResposta>> Renovar([FromBody] RenovarSessaoRequisicao requisicao, [FromServices] RenovarSessao casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }
}
