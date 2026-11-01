using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

/// <summary>
/// Valida e-mail e senha e abre uma sessão. A mensagem de erro é a mesma para e-mail
/// inexistente e senha errada, para não revelar quais e-mails têm conta.
/// </summary>
public sealed class FazerLogin
{
    private readonly IServicoDeContas _contas;
    private readonly IGrupoRepository _grupos;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly EmissorDeSessao _emissor;

    public FazerLogin(IServicoDeContas contas, IGrupoRepository grupos, IUnidadeDeTrabalho unidadeDeTrabalho, EmissorDeSessao emissor)
    {
        _contas = contas;
        _grupos = grupos;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _emissor = emissor;
    }

    public async Task<SessaoResposta> ExecutarAsync(LoginRequisicao requisicao, CancellationToken ct)
    {
        var email = (requisicao.Email ?? string.Empty).Trim().ToLowerInvariant();
        var conta = await _contas.ValidarCredenciaisAsync(email, requisicao.Senha ?? string.Empty, ct)
            ?? throw new ErroDeAutenticacao("E-mail ou senha incorretos.");

        var grupoId = await _grupos.ObterGrupoPessoalDoUsuarioAsync(conta.Id, ct)
            ?? throw new ErroDeAutenticacao("Conta sem grupo. Entre em contato com o suporte.");

        var sessao = await _emissor.EmitirAsync(conta, grupoId, ct);
        await _unidadeDeTrabalho.SalvarAsync(ct);
        return sessao;
    }
}
