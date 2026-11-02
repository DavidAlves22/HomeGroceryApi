using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.Servicos;

/// <summary>
/// Validações de entrada compartilhadas pelos casos de uso. A API é a última barreira: o app valida
/// antes só para dar resposta rápida, mas nunca se confia nisso.
/// </summary>
public static class Entrada
{
    public const int TamanhoMaximoDoNome = 120;
    public const decimal QuantidadeMaxima = 999999.999m;

    public static string Nome(string? texto, string rotulo = "O nome")
    {
        var nome = (texto ?? string.Empty).Trim();
        if (nome.Length == 0) throw new ErroDeValidacao($"{rotulo} é obrigatório.");
        if (nome.Length > TamanhoMaximoDoNome) throw new ErroDeValidacao($"{rotulo} pode ter no máximo {TamanhoMaximoDoNome} caracteres.");
        return nome;
    }

    public static decimal Quantidade(decimal quantidade)
    {
        if (quantidade <= 0 || quantidade > QuantidadeMaxima) throw new ErroDeValidacao("Informe uma quantidade maior que zero.");
        if (decimal.Round(quantidade, 3) != quantidade) throw new ErroDeValidacao("A quantidade aceita no máximo 3 casas decimais.");
        return quantidade;
    }
}

/// <summary>
/// Resolve produto e categoria de um item que o usuário digitou: reaproveita o produto de mesmo nome
/// (sem diferenciar maiúsculas) ou cria um novo, o que evita cadastros duplicados.
/// </summary>
public sealed class CatalogoDeProdutos
{
    private readonly ICategoriaRepository _categorias;
    private readonly IProdutoRepository _produtos;
    private readonly IRelogio _relogio;

    public CatalogoDeProdutos(ICategoriaRepository categorias, IProdutoRepository produtos, IRelogio relogio)
    {
        _categorias = categorias;
        _produtos = produtos;
        _relogio = relogio;
    }

    public async Task<(Produto Produto, Categoria Categoria)> ResolverAsync(Guid grupoId, string nome, Guid categoriaId, UnidadeDeMedidaEnum unidade, CancellationToken ct)
    {
        var categoria = await _categorias.ObterAsync(grupoId, categoriaId, ct);
        if (categoria is null || categoria.ExcluidoEm is not null) throw new ErroDeValidacao("Escolha uma categoria válida.");

        var produto = await _produtos.ObterPorNomeAsync(grupoId, nome, ct);
        if (produto is null)
        {
            produto = Produto.Criar(Guid.CreateVersion7(), grupoId, nome, categoria.Id, unidade, _relogio.AgoraUtc);
            await _produtos.AdicionarAsync(produto, ct);
        }

        return (produto, categoria);
    }
}

/// <summary>
/// Conversão de entidades em respostas da API (a API nunca expõe entidades diretamente).
/// </summary>
public static class Mapeamentos
{
    public static CategoriaResposta ParaResposta(this Categoria c) => new(c.Id, c.Nome, c.Ordem, c.Padrao);

    public static ProdutoResposta ParaResposta(this Produto p) => new(p.Id, p.Nome, p.CategoriaId, p.UnidadePadrao, p.Descricao, p.Favorito);

    public static ItemDoModeloResposta ParaResposta(this ItemDoModelo i, IReadOnlyDictionary<Guid, Categoria> categorias)
    {
        var nome = categorias.TryGetValue(i.CategoriaId, out var c) ? c.Nome : "Outros";
        return new ItemDoModeloResposta(i.Id, i.Nome, i.CategoriaId, nome, i.Unidade, i.QuantidadePadrao, i.Ordem);
    }

    public static ItemDaListaResposta ParaResposta(this ItemDaLista i)
    {
        return new ItemDaListaResposta(i.Id, i.NomeSnapshot, i.CategoriaId, i.CategoriaNomeSnapshot, i.UnidadeSnapshot, i.QuantidadePlanejada, i.Comprado, i.CompradoEm, i.CompradoPorUsuarioId, i.AdicionadoPorUsuarioId, i.Ordem, i.Versao);
    }

    public static ListaResumoResposta ParaResposta(this ResumoDeLista r)
    {
        return new ListaResumoResposta(r.Id, r.Competencia, Competencia.Parse(r.Competencia).Nome, r.Nome, r.Principal, r.Status, r.FinalizadaEm, r.TotalDeItens, r.Comprados, RegrasDeLista.PercentualConcluido(r.TotalDeItens, r.Comprados));
    }

    public static ResumoDeLista ParaResumo(this ListaDeCompras lista, IReadOnlyCollection<ItemDaLista> itens)
    {
        return new ResumoDeLista(lista.Id, lista.Competencia, lista.Nome, lista.Principal, lista.Status, lista.FinalizadaEm, itens.Count, itens.Count(i => i.Comprado));
    }
}
