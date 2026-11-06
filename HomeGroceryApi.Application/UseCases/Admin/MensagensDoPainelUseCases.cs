
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.UseCases.Conta;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Excecoes;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Admin;

/// <summary>Caixa de entrada do administrador: as mensagens de todas as pessoas, com o e-mail de contato atual.</summary>
public sealed class ListarMensagensDoPainel
{
    public const int Limite = 200;

    private readonly IContatoRepository _contato;

    public ListarMensagensDoPainel(IContatoRepository contato)
    {
        _contato = contato;
    }

    public async Task<AdminMensagensResposta> ExecutarAsync(CancellationToken ct)
    {
        var itens = await _contato.ListarTodasAsync(Limite, ct);
        var informacoes = (await _contato.ListarInformacoesAsync(ct)).Select(InformacoesDoContato.Converter).ToList();
        return new AdminMensagensResposta(informacoes, await _contato.ContarAguardandoRespostaAsync(ct), itens.Select(Converter).ToList());
    }

    internal static AdminMensagemResposta Converter(MensagemDeContato m) => new(m.Id, m.UsuarioId, m.NomeDoUsuario, m.EmailDoUsuario, m.Tipo, m.Assunto, m.Texto, m.Status, m.CriadoEm, m.AtualizadoEm, m.Resposta, m.RespondidoEm);
}

/// <summary>Responde e/ou muda o status de uma mensagem. A resposta nova aparece para a pessoa como "resposta nova".</summary>
public sealed class ResponderMensagemDoPainel
{
    private readonly IContatoRepository _contato;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public ResponderMensagemDoPainel(IContatoRepository contato, IUnidadeDeTrabalho unidade, IRelogio relogio)
    {
        _contato = contato;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<AdminMensagemResposta> ExecutarAsync(Guid id, AdminResponderMensagemRequisicao requisicao, CancellationToken ct)
    {
        if (!Enum.IsDefined(requisicao.Status)) throw new ErroDeValidacao("Status inválido.");
        var resposta = requisicao.Resposta?.Trim();
        if (resposta is { Length: > MensagemDeContato.RespostaMaxima }) throw new ErroDeValidacao($"A resposta pode ter no máximo {MensagemDeContato.RespostaMaxima} caracteres.");

        var mensagem = await _contato.ObterAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Mensagem não encontrada.");
        try
        {
            mensagem.Responder(resposta, requisicao.Status, _relogio.AgoraUtc);
        }
        catch (ErroDeDominio erro)
        {
            // Status "Respondida" sem texto é pedido mal formado, não regra de negócio violada: vira 400.
            throw new ErroDeValidacao(erro.Message);
        }

        await _unidade.SalvarAsync(ct);
        return ListarMensagensDoPainel.Converter(mensagem);
    }
}
