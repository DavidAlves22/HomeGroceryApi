using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

public sealed record CriarListaRequisicao(string Competencia, string? Nome, OrigemDaListaEnum Origem, IReadOnlyList<Guid>? ItensDoModeloIds, bool PermitirDuplicada);

public sealed record ItemDaListaRequisicao(string Nome, Guid CategoriaId, UnidadeDeMedidaEnum Unidade, decimal Quantidade);

public sealed record EditarItemDaListaRequisicao(string Nome, Guid CategoriaId, UnidadeDeMedidaEnum Unidade, decimal Quantidade, int Versao);

public sealed record MarcarCompradoRequisicao(bool Comprado);

public sealed record ItemDaListaResposta(
    Guid Id,
    string Nome,
    Guid? CategoriaId,
    string CategoriaNome,
    UnidadeDeMedidaEnum Unidade,
    decimal Quantidade,
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
    int Percentual);

public sealed record ListaResposta(ListaResumoResposta Resumo, IReadOnlyList<ItemDaListaResposta> Itens);
