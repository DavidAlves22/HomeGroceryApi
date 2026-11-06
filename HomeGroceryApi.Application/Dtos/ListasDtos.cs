using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

public sealed record CriarListaRequisicao(string Competencia, string? Nome, OrigemDaListaEnum Origem, IReadOnlyList<Guid>? ItensDoModeloIds, bool PermitirDuplicada, Guid? ListaDeOrigemId = null, IReadOnlyList<Guid>? ItensDeOrigemIds = null, IReadOnlyList<ItemSugeridoRequisicao>? ItensSugeridos = null);

/// <summary>Chave vem de GET /listas/sugestao; Quantidade vazia = a sugerida pelo histórico.</summary>
public sealed record ItemSugeridoRequisicao(string Chave, decimal? Quantidade = null);

public sealed record ItemDaListaRequisicao(string Nome, Guid CategoriaId, UnidadeDeMedidaEnum Unidade, decimal Quantidade, decimal? PrecoEstimado = null);

public sealed record EditarItemDaListaRequisicao(
    string Nome,
    Guid CategoriaId,
    UnidadeDeMedidaEnum Unidade,
    decimal Quantidade,
    int Versao,
    decimal? QuantidadeComprada = null,
    decimal? PrecoEstimado = null,
    decimal? PrecoUnitario = null);

/// <summary>LojaId é o mercado onde foi comprado (compra dividida entre mercados). Vazio é aceito: o item fica sem mercado.</summary>
public sealed record MarcarCompradoRequisicao(bool Comprado, Guid? LojaId = null);

public sealed record SemNaLojaRequisicao(Guid LojaId, bool Sem);

public sealed record ItemDaListaResposta(
    Guid Id,
    Guid? ProdutoId,
    string Nome,
    Guid? CategoriaId,
    string CategoriaNome,
    UnidadeDeMedidaEnum Unidade,
    decimal Quantidade,
    decimal? QuantidadeComprada,
    decimal? PrecoEstimado,
    decimal? PrecoUnitario,
    decimal? PrecoReferencia,
    decimal TotalEstimado,
    decimal TotalRealizado,
    bool Comprado,
    DateTime? CompradoEm,
    Guid? CompradoPorUsuarioId,
    Guid AdicionadoPorUsuarioId,
    int Ordem,
    int Versao,
    Guid? CompradoNaLojaId,
    IReadOnlyList<Guid> SemNasLojas);

public sealed record ListaResumoResposta(
    Guid Id,
    string Competencia,
    string CompetenciaNome,
    string Nome,
    bool Principal,
    StatusDaListaEnum Status,
    DateTime? FinalizadaEm,
    int TotalDeItens,
    int Comprados,
    int Percentual,
    decimal TotalEstimado,
    decimal TotalRealizado);

public sealed record TotaisPorCategoriaResposta(string Categoria, decimal Estimado, decimal Realizado, int Itens, int Comprados);

public sealed record TotaisResposta(decimal Estimado, decimal Realizado, decimal Restante, int Itens, int Comprados, int CompradosSemPreco, IReadOnlyList<TotaisPorCategoriaResposta> PorCategoria);

/// <summary>Quem pega a categoria nesta ida ao mercado.</summary>
public sealed record DivisaoDaCategoriaResposta(Guid CategoriaId, Guid UsuarioId);

public sealed record ListaResposta(ListaResumoResposta Resumo, IReadOnlyList<ItemDaListaResposta> Itens, TotaisResposta Totais, IReadOnlyList<DivisaoDaCategoriaResposta> Divisao);

/// <summary>Substitui a divisao inteira; lista vazia desfaz. Categoria que nao aparece e de todos.</summary>
public sealed record DividirCompraRequisicao(IReadOnlyList<DivisaoDaCategoriaResposta>? Atribuicoes);
