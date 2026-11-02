using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

/// <summary>
/// Troca um token de renovação válido por uma sessão nova. O token antigo é revogado
/// (rotação): cada token de renovação só serve uma vez.
/// </summary>
public sealed class RenovarSessao
{
    private readonly ITokenService _tokens;
    private readonly ITokenDeRenovacaoRepository _tokensDeRenovacao;
    private readonly IServicoDeContas _contas;
    private readonly ServicoDeGrupoAtivo _grupoAtivo;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly EmissorDeSessao _emissor;
    private readonly IRelogio _relogio;

    public RenovarSessao(ITokenService tokens, ITokenDeRenovacaoRepository tokensDeRenovacao, IServicoDeContas contas, ServicoDeGrupoAtivo grupoAtivo, IUnidadeDeTrabalho unidadeDeTrabalho, EmissorDeSessao emissor, IRelogio relogio)
    {
        _tokens = tokens;
        _tokensDeRenovacao = tokensDeRenovacao;
        _contas = contas;
        _grupoAtivo = grupoAtivo;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _emissor = emissor;
        _relogio = relogio;
    }

    public async Task<SessaoResposta> ExecutarAsync(RenovarSessaoRequisicao requisicao, CancellationToken ct)
    {
        const string mensagem = "Sessão expirada. Entre novamente.";
        if (string.IsNullOrWhiteSpace(requisicao.TokenDeRenovacao)) throw new ErroDeAutenticacao(mensagem);

        var agora = _relogio.AgoraUtc;
        var registro = await _tokensDeRenovacao.ObterPorHashAsync(_tokens.CalcularHash(requisicao.TokenDeRenovacao), ct);
        if (registro is null || !registro.EstaValido(agora)) throw new ErroDeAutenticacao(mensagem);

        var conta = await _contas.ObterPorIdAsync(registro.UsuarioId, ct) ?? throw new ErroDeAutenticacao(mensagem);
        var grupoId = await _grupoAtivo.ResolverAsync(conta, ct);

        registro.Revogar(agora);
        var sessao = await _emissor.EmitirAsync(conta, grupoId, ct);
        await _unidadeDeTrabalho.SalvarAsync(ct);
        return sessao;
    }
}
