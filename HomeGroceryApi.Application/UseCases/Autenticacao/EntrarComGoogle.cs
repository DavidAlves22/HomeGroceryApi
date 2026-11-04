using System.Security.Cryptography;
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

/// <summary>Diz ao app se o login com Google está ligado e com qual Client ID montar o botão (o ID é público).</summary>
public sealed class ConsultarLoginComGoogle
{
    private readonly IValidadorDeLoginGoogle _validador;

    public ConsultarLoginComGoogle(IValidadorDeLoginGoogle validador)
    {
        _validador = validador;
    }

    public GoogleDisponivelResposta Executar() => new(_validador.ClienteId);
}

/// <summary>
/// Entra com a conta Google. Conta já existente com o mesmo e-mail (o Google confirmou que a pessoa é dona dele) apenas
/// entra; e-mail novo cria a conta e o grupo pessoal, como no cadastro. A conta criada tem uma senha aleatória que ninguém
/// conhece: quem quiser entrar também com senha usa "Esqueci minha senha".
/// </summary>
public sealed class EntrarComGoogle
{
    private readonly IValidadorDeLoginGoogle _validador;
    private readonly IServicoDeContas _contas;
    private readonly IGrupoRepository _grupos;
    private readonly ServicoDeGrupoAtivo _grupoAtivo;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly EmissorDeSessao _emissor;
    private readonly IRelogio _relogio;

    public EntrarComGoogle(IValidadorDeLoginGoogle validador, IServicoDeContas contas, IGrupoRepository grupos, ServicoDeGrupoAtivo grupoAtivo, IUnidadeDeTrabalho unidadeDeTrabalho, EmissorDeSessao emissor, IRelogio relogio)
    {
        _validador = validador;
        _contas = contas;
        _grupos = grupos;
        _grupoAtivo = grupoAtivo;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _emissor = emissor;
        _relogio = relogio;
    }

    public async Task<SessaoResposta> ExecutarAsync(LoginGoogleRequisicao requisicao, CancellationToken ct)
    {
        var identidade = await _validador.ValidarAsync(requisicao.IdToken ?? string.Empty, ct) ?? throw new ErroDeAutenticacao("Não foi possível confirmar sua conta Google.");
        var email = identidade.Email.Trim().ToLowerInvariant();

        var conta = await _contas.ObterPorEmailAsync(email, ct);
        if (conta is not null)
        {
            var grupoId = await _grupoAtivo.ResolverAsync(conta, ct);
            var sessaoExistente = await _emissor.EmitirAsync(conta, grupoId, ct);
            await _unidadeDeTrabalho.SalvarAsync(ct);
            return sessaoExistente;
        }

        SessaoResposta? sessao = null;
        var nome = NomeValido(identidade.Nome, email);

        await _unidadeDeTrabalho.ExecutarEmTransacaoAsync(async () =>
        {
            var criacao = await _contas.CriarAsync(email, nome, SenhaAleatoria(), ct);
            if (!criacao.Sucesso) throw new ErroDeAutenticacao("Não foi possível criar sua conta com o Google.");

            var usuarioId = criacao.UsuarioId!.Value;
            var agora = _relogio.AgoraUtc;
            var grupo = Grupo.CriarPessoal(Guid.CreateVersion7(), nome, agora);
            var membro = MembroDoGrupo.Criar(grupo.Id, usuarioId, PapelNoGrupoEnum.Administrador, agora);

            await _grupos.AdicionarAsync(grupo, membro, CategoriasIniciais.CriarPara(grupo.Id, Guid.CreateVersion7), ct);
            sessao = await _emissor.EmitirAsync(new ContaDoUsuario(usuarioId, nome, email), grupo.Id, ct);
            await _unidadeDeTrabalho.SalvarAsync(ct);
        }, ct);

        return sessao!;
    }

    /// <summary>O nome do Google serve se couber na regra do app (2 a 80 caracteres); senão usa a parte do e-mail antes do @.</summary>
    private static string NomeValido(string? nomeDoGoogle, string email)
    {
        var nome = (nomeDoGoogle ?? string.Empty).Trim();
        if (nome.Length < 2) nome = email.Split('@')[0];
        if (nome.Length < 2) nome = "Usuário";
        return nome.Length > 80 ? nome[..80] : nome;
    }

    /// <summary>Satisfaz a regra de senha do Identity (maiúscula, minúscula, número, símbolo) com 256 bits aleatórios.</summary>
    private static string SenhaAleatoria() => "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
