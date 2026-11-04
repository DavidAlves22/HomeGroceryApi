using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
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
