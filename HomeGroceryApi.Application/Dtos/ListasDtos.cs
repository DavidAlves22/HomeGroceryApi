using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

public sealed record CriarListaRequisicao(string Competencia, string? Nome, OrigemDaListaEnum Origem, IReadOnlyList<Guid>? ItensDoModeloIds, bool PermitirDuplicada, Guid? ListaDeOrigemId = null, IReadOnlyList<Guid>? ItensDeOrigemIds = null);

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

public sealed record MarcarCompradoRequisicao(bool Comprado);

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
    int Versao);

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

public sealed record ListaResposta(ListaResumoResposta Resumo, IReadOnlyList<ItemDaListaResposta> Itens, TotaisResposta Totais);
