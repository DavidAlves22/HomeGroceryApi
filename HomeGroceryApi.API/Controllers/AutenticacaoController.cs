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

    /// <summary>O app só mostra "Entrar com Google" quando há Client ID configurado.</summary>
    [HttpGet("google")]
    public ActionResult<GoogleDisponivelResposta> GoogleDisponivel([FromServices] ConsultarLoginComGoogle casoDeUso)
    {
        return Ok(casoDeUso.Executar());
    }

    [HttpPost("login-google")]
    public async Task<ActionResult<SessaoResposta>> EntrarComGoogle([FromBody] LoginGoogleRequisicao requisicao, [FromServices] EntrarComGoogle casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    /// <summary>O app só mostra "Esqueci minha senha" quando o servidor consegue enviar e-mail.</summary>
    [HttpGet("recuperacao")]
    public ActionResult<RecuperacaoResposta> RecuperacaoDisponivel([FromServices] ConsultarRecuperacaoDeSenha casoDeUso)
    {
        return Ok(casoDeUso.Executar());
    }

    /// <summary>Envia um código de 6 dígitos por e-mail. Responde 204 exista ou não a conta.</summary>
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciASenha([FromBody] EsqueciSenhaRequisicao requisicao, [FromServices] SolicitarRecuperacaoDeSenha casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }

    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> Redefinir([FromBody] RedefinirSenhaRequisicao requisicao, [FromServices] RedefinirSenha casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(requisicao, ct);
        return NoContent();
    }

    [HttpPost("renovar")]
    public async Task<ActionResult<SessaoResposta>> Renovar([FromBody] RenovarSessaoRequisicao requisicao, [FromServices] RenovarSessao casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }
}
