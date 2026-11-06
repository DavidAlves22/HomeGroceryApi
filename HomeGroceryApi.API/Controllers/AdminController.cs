using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Admin;
using HomeGroceryApi.Infrastructure.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

/// <summary>
/// Painel de administração. O papel Admin é conferido no servidor a cada chamada (claim assinada no JWT): quem não é
/// administrador recebe 403 mesmo conhecendo a URL.
/// </summary>
[ApiController]
[Authorize(Roles = TokenService.PapelAdmin)]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("resumo")]
    public async Task<ActionResult<AdminResumoResposta>> Resumo([FromServices] ConsultarResumoDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("series")]
    public async Task<ActionResult<AdminSeriesResposta>> Series([FromQuery] int dias, [FromServices] ConsultarSeriesDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(dias, ct));
    }

    [HttpGet("insights")]
    public async Task<ActionResult<AdminInsightsResposta>> Insights([FromServices] ConsultarInsightsDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<AdminUsuariosResposta>> Usuarios([FromQuery] int pagina, [FromServices] ListarUsuariosDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(pagina, ct));
    }

    [HttpDelete("usuarios/{id:guid}")]
    public async Task<IActionResult> ExcluirConta(Guid id, [FromServices] ExcluirContaPeloAdmin casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    [HttpGet("mapas")]
    public async Task<ActionResult<AdminMapasResposta>> Mapas([FromServices] ListarMapasDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpGet("mapas/{id:guid}")]
    public async Task<ActionResult<CatalogoMercadoDetalheResposta>> Mapa(Guid id, [FromServices] ObterMapaDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, ct));
    }

    [HttpDelete("mapas/{id:guid}")]
    public async Task<IActionResult> ExcluirMapa(Guid id, [FromServices] ExcluirMapaPeloAdmin casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    [HttpGet("mensagens")]
    public async Task<ActionResult<AdminMensagensResposta>> Mensagens([FromServices] ListarMensagensDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(ct));
    }

    [HttpPut("mensagens/{id:guid}")]
    public async Task<ActionResult<AdminMensagemResposta>> ResponderMensagem(Guid id, [FromBody] AdminResponderMensagemRequisicao requisicao, [FromServices] ResponderMensagemDoPainel casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpPost("contato/informacoes")]
    public async Task<ActionResult<InformacaoDeContatoResposta>> AdicionarInformacao([FromBody] InformacaoDeContatoRequisicao requisicao, [FromServices] AdicionarInformacaoDeContato casoDeUso, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await casoDeUso.ExecutarAsync(requisicao, ct));
    }

    [HttpPut("contato/informacoes/{id:guid}")]
    public async Task<ActionResult<InformacaoDeContatoResposta>> AlterarInformacao(Guid id, [FromBody] InformacaoDeContatoRequisicao requisicao, [FromServices] AlterarInformacaoDeContato casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(id, requisicao, ct));
    }

    [HttpDelete("contato/informacoes/{id:guid}")]
    public async Task<IActionResult> RemoverInformacao(Guid id, [FromServices] RemoverInformacaoDeContato casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, ct);
        return NoContent();
    }

    [HttpPut("usuarios/{id:guid}/bloqueio")]
    public async Task<IActionResult> Bloqueio(Guid id, [FromBody] AdminBloqueioRequisicao requisicao, [FromServices] DefinirBloqueioDeConta casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(id, requisicao.Bloqueado, ct);
        return NoContent();
    }
}
