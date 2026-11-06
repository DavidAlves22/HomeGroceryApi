using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.UseCases.Conta;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Admin;

internal static class EntradaDeInformacao
{
    /// <summary>Título e valor são obrigatórios; a descrição é opcional (vazia vira nula, e a tela não mostra a linha).</summary>
    public static (string Titulo, string? Descricao, string Valor) Validar(InformacaoDeContatoRequisicao requisicao)
    {
        var titulo = (requisicao.Titulo ?? string.Empty).Trim();
        var descricao = (requisicao.Descricao ?? string.Empty).Trim();
        var valor = (requisicao.Valor ?? string.Empty).Trim();

        if (titulo.Length == 0) throw new ErroDeValidacao("Informe o título.");
        if (titulo.Length > InformacaoDeContato.TituloMaximo) throw new ErroDeValidacao($"O título pode ter no máximo {InformacaoDeContato.TituloMaximo} caracteres.");
        if (descricao.Length > InformacaoDeContato.DescricaoMaxima) throw new ErroDeValidacao($"A descrição pode ter no máximo {InformacaoDeContato.DescricaoMaxima} caracteres.");
        if (valor.Length == 0) throw new ErroDeValidacao("Informe o valor.");
        if (valor.Length > InformacaoDeContato.ValorMaximo) throw new ErroDeValidacao($"O valor pode ter no máximo {InformacaoDeContato.ValorMaximo} caracteres.");

        return (titulo, descricao.Length == 0 ? null : descricao, valor);
    }
}

/// <summary>Cadastra uma informação nova no card de contato (vai para o fim da lista).</summary>
public sealed class AdicionarInformacaoDeContato
{
    public const int Maximo = 20;

    private readonly IContatoRepository _contato;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public AdicionarInformacaoDeContato(IContatoRepository contato, IUnidadeDeTrabalho unidade, IRelogio relogio)
    {
        _contato = contato;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<InformacaoDeContatoResposta> ExecutarAsync(InformacaoDeContatoRequisicao requisicao, CancellationToken ct)
    {
        var (titulo, descricao, valor) = EntradaDeInformacao.Validar(requisicao);

        var existentes = await _contato.ListarInformacoesAsync(ct);
        if (existentes.Count >= Maximo) throw new ErroDeValidacao($"O card de contato aceita até {Maximo} informações.");

        var informacao = InformacaoDeContato.Criar(Guid.CreateVersion7(), titulo, descricao, valor, existentes.Count == 0 ? 1 : existentes.Max(i => i.Ordem) + 1, _relogio.AgoraUtc);
        await _contato.AdicionarInformacaoAsync(informacao, ct);
        await _unidade.SalvarAsync(ct);
        return InformacoesDoContato.Converter(informacao);
    }
}

public sealed class AlterarInformacaoDeContato
{
    private readonly IContatoRepository _contato;
    private readonly IUnidadeDeTrabalho _unidade;

    public AlterarInformacaoDeContato(IContatoRepository contato, IUnidadeDeTrabalho unidade)
    {
        _contato = contato;
        _unidade = unidade;
    }

    public async Task<InformacaoDeContatoResposta> ExecutarAsync(Guid id, InformacaoDeContatoRequisicao requisicao, CancellationToken ct)
    {
        var (titulo, descricao, valor) = EntradaDeInformacao.Validar(requisicao);
        var informacao = await _contato.ObterInformacaoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Informação não encontrada.");

        informacao.Alterar(titulo, descricao, valor);
        await _unidade.SalvarAsync(ct);
        return InformacoesDoContato.Converter(informacao);
    }
}

public sealed class RemoverInformacaoDeContato
{
    private readonly IContatoRepository _contato;
    private readonly IUnidadeDeTrabalho _unidade;

    public RemoverInformacaoDeContato(IContatoRepository contato, IUnidadeDeTrabalho unidade)
    {
        _contato = contato;
        _unidade = unidade;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var informacao = await _contato.ObterInformacaoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Informação não encontrada.");

        _contato.RemoverInformacao(informacao);
        await _unidade.SalvarAsync(ct);
    }
}
