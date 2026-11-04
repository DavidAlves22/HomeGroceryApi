using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

/// <summary>
/// Um item da nota fiscal já lido pelo app. `ItemId` aponta para um item da lista (recebe preço e quantidade e vira comprado);
/// sem `ItemId` é item novo e precisa de `CategoriaId`.
/// </summary>
public sealed record ItemDaNotaRequisicao(string? Codigo, string? Nome, decimal Quantidade, decimal PrecoUnitario, Guid? ItemId, Guid? CategoriaId, UnidadeDeMedidaEnum? Unidade);

public sealed record ImportarNotaRequisicao(string? Cnpj, Guid? LojaId, IReadOnlyList<ItemDaNotaRequisicao>? Itens, string? ChaveDeAcesso = null);

public sealed record VinculoDaNotaResposta(string Codigo, Guid ProdutoId);

public sealed record NotaImportadaResposta(bool Importada);
