using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

public sealed record NomeRequisicao(string Nome);

public sealed record FavoritoRequisicao(bool Favorito);

public sealed record ReordenarRequisicao(IReadOnlyList<Guid> Ids);

public sealed record CategoriaResposta(Guid Id, string Nome, int Ordem, bool Padrao);

public sealed record ProdutoResposta(Guid Id, string Nome, Guid CategoriaId, UnidadeDeMedidaEnum UnidadePadrao, string? Descricao, bool Favorito);

public sealed record EditarProdutoRequisicao(string Nome, Guid CategoriaId, UnidadeDeMedidaEnum UnidadePadrao, string? Descricao);

public sealed record ItemDoModeloRequisicao(string Nome, Guid CategoriaId, UnidadeDeMedidaEnum Unidade, decimal Quantidade);

public sealed record ItemDoModeloResposta(Guid Id, string Nome, Guid CategoriaId, string CategoriaNome, UnidadeDeMedidaEnum Unidade, decimal Quantidade, int Ordem);
