using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Conta;

/// <summary>As informações de contato que o administrador cadastrou. Qualquer pessoa logada pode ver.</summary>
public sealed class ObterContato
{
    private readonly IContatoRepository _contato;

    public ObterContato(IContatoRepository contato)
    {
        _contato = contato;
    }

    public async Task<ContatoResposta> ExecutarAsync(CancellationToken ct)
    {
        return new ContatoResposta((await _contato.ListarInformacoesAsync(ct)).Select(InformacoesDoContato.Converter).ToList());
    }
}

internal static class InformacoesDoContato
{
    public static InformacaoDeContatoResposta Converter(InformacaoDeContato i) => new(i.Id, i.Titulo, i.Descricao, i.Valor);
}

/// <summary>As mensagens da própria pessoa (nunca as de outra), da mais recente para a mais antiga.</summary>
public sealed class ListarMinhasMensagens
{
    public const int Limite = 100;

    private readonly IContatoRepository _contato;
    private readonly IUsuarioAtual _usuario;

    public ListarMinhasMensagens(IContatoRepository contato, IUsuarioAtual usuario)
    {
        _contato = contato;
        _usuario = usuario;
    }

    public async Task<MensagensResposta> ExecutarAsync(CancellationToken ct)
    {
        var itens = (await _contato.ListarDoUsuarioAsync(_usuario.UsuarioId, Limite, ct)).Select(EnviarMensagemDeContato.Converter).ToList();
        return new MensagensResposta(itens, itens.Count(i => i.RespostaNova));
    }
}

/// <summary>
/// Envia um feedback ou solicitação. Assunto e texto têm limite de tamanho e cada pessoa pode mandar poucas por dia: a
/// caixa é do administrador, e sem esse freio uma conta poderia enchê-la (e o banco gratuito) de lixo.
/// </summary>
public sealed class EnviarMensagemDeContato
{
    public const int MaximoPorDia = 10;
    public const int TextoMinimo = 5;
    public const int AssuntoMinimo = 3;

    private readonly IContatoRepository _contato;
    private readonly IServicoDeContas _contas;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public EnviarMensagemDeContato(IContatoRepository contato, IServicoDeContas contas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _contato = contato;
        _contas = contas;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task<MensagemResposta> ExecutarAsync(EnviarMensagemRequisicao requisicao, CancellationToken ct)
    {
        if (!Enum.IsDefined(requisicao.Tipo)) throw new ErroDeValidacao("Escolha se é um feedback ou uma solicitação.");

        var assunto = (requisicao.Assunto ?? string.Empty).Trim();
        if (assunto.Length < AssuntoMinimo) throw new ErroDeValidacao($"Escreva um assunto com pelo menos {AssuntoMinimo} letras.");
        if (assunto.Length > MensagemDeContato.AssuntoMaximo) throw new ErroDeValidacao($"O assunto pode ter no máximo {MensagemDeContato.AssuntoMaximo} caracteres.");

        var texto = (requisicao.Texto ?? string.Empty).Trim();
        if (texto.Length < TextoMinimo) throw new ErroDeValidacao($"Escreva a mensagem com pelo menos {TextoMinimo} letras.");
        if (texto.Length > MensagemDeContato.TextoMaximo) throw new ErroDeValidacao($"A mensagem pode ter no máximo {MensagemDeContato.TextoMaximo} caracteres.");

        var agora = _relogio.AgoraUtc;
        if (await _contato.ContarEnviadasDesdeAsync(_usuario.UsuarioId, agora.AddDays(-1), ct) >= MaximoPorDia) throw new ErroDeValidacao($"Você já enviou {MaximoPorDia} mensagens hoje. Tente de novo amanhã.");

        var conta = await _contas.ObterPorIdAsync(_usuario.UsuarioId, ct) ?? throw new ErroDeNaoEncontrado("Conta não encontrada.");
        var mensagem = MensagemDeContato.Criar(Guid.CreateVersion7(), conta.Id, conta.Nome, conta.Email, requisicao.Tipo, assunto, texto, agora);
        await _contato.AdicionarAsync(mensagem, ct);
        await _unidade.SalvarAsync(ct);
        return Converter(mensagem);
    }

    internal static MensagemResposta Converter(MensagemDeContato m) => new(m.Id, m.Tipo, m.Assunto, m.Texto, m.Status, m.CriadoEm, m.AtualizadoEm, m.Resposta, m.RespondidoEm, m.TemRespostaNova);
}

/// <summary>A pessoa abriu a resposta: a tela deixa de marcar como nova. Só vale para mensagem dela (de outra pessoa: 404).</summary>
public sealed class MarcarRespostaComoVista
{
    private readonly IContatoRepository _contato;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public MarcarRespostaComoVista(IContatoRepository contato, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _contato = contato;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var mensagem = await _contato.ObterAsync(id, ct);
        if (mensagem is null || mensagem.UsuarioId != _usuario.UsuarioId) throw new ErroDeNaoEncontrado("Mensagem não encontrada.");

        mensagem.MarcarRespostaComoVista(_relogio.AgoraUtc);
        await _unidade.SalvarAsync(ct);
    }
}
