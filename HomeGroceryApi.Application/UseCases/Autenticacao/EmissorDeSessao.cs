using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

/// <summary>
/// Monta uma sessão completa (token de acesso + token de renovação) para um usuário.
/// Cadastro, login e renovação compartilham isto para não repetir a emissão.
/// </summary>
public sealed class EmissorDeSessao
{
    private readonly ITokenService _tokens;
    private readonly ITokenDeRenovacaoRepository _tokensDeRenovacao;
    private readonly IServicoDeContas _contas;
    private readonly IAdministradores _administradores;
    private readonly IRelogio _relogio;

    public EmissorDeSessao(ITokenService tokens, ITokenDeRenovacaoRepository tokensDeRenovacao, IServicoDeContas contas, IAdministradores administradores, IRelogio relogio)
    {
        _tokens = tokens;
        _tokensDeRenovacao = tokensDeRenovacao;
        _contas = contas;
        _administradores = administradores;
        _relogio = relogio;
    }

    public async Task<SessaoResposta> EmitirAsync(ContaDoUsuario conta, Guid grupoId, CancellationToken ct)
    {
        var acesso = _tokens.GerarTokenDeAcesso(conta.Id, conta.Email, grupoId);
        var renovacao = _tokens.GerarTokenDeRenovacao();

        var registro = TokenDeRenovacao.Criar(Guid.CreateVersion7(), conta.Id, _tokens.CalcularHash(renovacao.Token), renovacao.ExpiraEm);
        await _tokensDeRenovacao.AdicionarAsync(registro, ct);
        await _contas.RegistrarAcessoAsync(conta.Id, _relogio.AgoraUtc, ct);

        // Mesmo critério do token (TokenService); serve só para o app decidir se mostra o painel. A proteção real é a da API.
        return new SessaoResposta(acesso.Token, acesso.ExpiraEm, renovacao.Token, new UsuarioResposta(conta.Id, conta.Nome, conta.Email), grupoId, _administradores.EhAdministrador(conta.Email));
    }
}
