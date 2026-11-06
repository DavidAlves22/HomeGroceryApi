using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Conta;

/// <summary>
/// Troca o nome de quem está logado. O nome aparece para a família ("Ana marcou", membros do grupo), então os grupos
/// dessa pessoa são avisados em tempo real; falhar em avisar nunca desfaz a troca.
/// </summary>
public sealed class AlterarNomeDoUsuario
{
    private readonly IServicoDeContas _contas;
    private readonly IGrupoRepository _grupos;
    private readonly INotificador _notificador;
    private readonly IUsuarioAtual _usuario;

    public AlterarNomeDoUsuario(IServicoDeContas contas, IGrupoRepository grupos, INotificador notificador, IUsuarioAtual usuario)
    {
        _contas = contas;
        _grupos = grupos;
        _notificador = notificador;
        _usuario = usuario;
    }

    public async Task<UsuarioResposta> ExecutarAsync(AlterarNomeRequisicao requisicao, CancellationToken ct)
    {
        var nome = (requisicao.Nome ?? string.Empty).Trim();
        if (nome.Length is < 2 or > 80) throw new ErroDeValidacao("Informe um nome entre 2 e 80 caracteres.");

        var conta = await _contas.ObterPorIdAsync(_usuario.UsuarioId, ct) ?? throw new ErroDeNaoEncontrado("Conta não encontrada.");
        await _contas.AlterarNomeAsync(conta.Id, nome, ct);

        try
        {
            foreach (var vinculo in await _grupos.ListarVinculosDoUsuarioAsync(conta.Id, ct)) await _notificador.PublicarNoGrupoAsync(vinculo.GrupoId, new EventoDeAlteracao(EventoDeAlteracao.Membros, PorUsuarioId: conta.Id), ct);
        }
        catch
        {
            // O nome já foi trocado: quem não receber o aviso vê o nome novo ao recarregar.
        }

        return new UsuarioResposta(conta.Id, nome, conta.Email);
    }
}

/// <summary>
/// O próprio dono encerra a conta e leva embora os dados dele (ver <see cref="ServicoDeExclusaoDeConta"/>). Para ninguém
/// fazer isso por engano nem com um aparelho emprestado: precisa digitar o e-mail da conta e, quando a conta tem senha
/// própria, a senha. Conta criada pelo Google que nunca definiu senha confirma só pelo e-mail.
/// </summary>
public sealed class EncerrarConta
{
    private readonly IServicoDeContas _contas;
    private readonly IAdministradores _administradores;
    private readonly ServicoDeExclusaoDeConta _exclusao;
    private readonly IUsuarioAtual _usuario;

    public EncerrarConta(IServicoDeContas contas, IAdministradores administradores, ServicoDeExclusaoDeConta exclusao, IUsuarioAtual usuario)
    {
        _contas = contas;
        _administradores = administradores;
        _exclusao = exclusao;
        _usuario = usuario;
    }

    public async Task ExecutarAsync(EncerrarContaRequisicao requisicao, CancellationToken ct)
    {
        var conta = await _contas.ObterPorIdAsync(_usuario.UsuarioId, ct) ?? throw new ErroDeNaoEncontrado("Conta não encontrada.");
        if (_administradores.EhAdministrador(conta.Email)) throw new ErroDeValidacao("A conta de administrador não pode ser encerrada por aqui. Tire o e-mail da lista de administradores primeiro.");

        if (!string.Equals((requisicao.ConfirmacaoEmail ?? string.Empty).Trim(), conta.Email, StringComparison.OrdinalIgnoreCase)) throw new ErroDeValidacao("Digite o e-mail da conta para confirmar.");
        if (!conta.SemSenhaPropria && !await _contas.ConferirSenhaAsync(conta.Id, requisicao.Senha ?? string.Empty, ct)) throw new ErroDeValidacao("Senha incorreta.");

        await _exclusao.ExcluirAsync(conta.Id, ct);
    }
}
