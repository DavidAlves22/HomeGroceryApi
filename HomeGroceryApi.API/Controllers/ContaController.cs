using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.UseCases.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeGroceryApi.API.Controllers;

[ApiController]
[Authorize]
[Route("conta")]
public sealed class ContaController : ControllerBase
{
    /// <summary>Troca o nome de quem está logado (aparece para a família).</summary>
    [HttpPut("nome")]
    public async Task<ActionResult<UsuarioResposta>> AlterarNome([FromBody] AlterarNomeRequisicao requisicao, [FromServices] AlterarNomeDoUsuario casoDeUso, CancellationToken ct)
    {
        return Ok(await casoDeUso.ExecutarAsync(requisicao, ct));
    }
}
