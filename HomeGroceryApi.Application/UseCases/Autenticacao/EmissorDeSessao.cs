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

    public EmissorDeSessao(ITokenService tokens, ITokenDeRenovacaoRepository tokensDeRenovacao)
    {
        _tokens = tokens;
        _tokensDeRenovacao = tokensDeRenovacao;
    }

    public async Task<SessaoResposta> EmitirAsync(ContaDoUsuario conta, Guid grupoId, CancellationToken ct)
    {
        var acesso = _tokens.GerarTokenDeAcesso(conta.Id, conta.Email, grupoId);
        var renovacao = _tokens.GerarTokenDeRenovacao();

        var registro = TokenDeRenovacao.Criar(Guid.CreateVersion7(), conta.Id, _tokens.CalcularHash(renovacao.Token), renovacao.ExpiraEm);
        await _tokensDeRenovacao.AdicionarAsync(registro, ct);

        return new SessaoResposta(acesso.Token, acesso.ExpiraEm, renovacao.Token, new UsuarioResposta(conta.Id, conta.Nome, conta.Email), grupoId);
    }
}
