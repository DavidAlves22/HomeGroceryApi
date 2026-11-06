namespace HomeGroceryApi.Application.Dtos;

public sealed record GastoDoMesResposta(string Competencia, string CompetenciaNome, decimal Estimado, decimal Realizado, int Itens, int Comprados, decimal? VariacaoPercentual);

public sealed record GastoDaCategoriaResposta(string Categoria, decimal Total, decimal Percentual);

public sealed record ProdutoFrequenteResposta(string Nome, int Compras);

public sealed record PrecoBomResposta(string Nome, string UnidadeBase, decimal UltimoPreco, decimal PrecoMedio, int PercentualAbaixo, int Meses, string Descricao);

public sealed record ProdutoSazonalResposta(string Chave, string Nome, string CategoriaNome, string Unidade, IReadOnlyList<int> Anos, DateOnly UltimaCompraNoMes, string Descricao, Guid? CategoriaId);

public sealed record ParCasadoResposta(string Primeiro, string Segundo, int Juntos, string Descricao);

public sealed record PadroesDeCompraResposta(IReadOnlyList<PrecoBomResposta> PrecosBons, IReadOnlyList<ProdutoSazonalResposta> Sazonais, IReadOnlyList<ParCasadoResposta> Casados);

public sealed record MercadoHabitualResposta(string Chave, string Nome, Guid LojaId, int Compras, int Total);

public sealed record ResponsavelHabitualResposta(Guid CategoriaId, string Categoria, Guid UsuarioId, int Compras, int Total);

public sealed record HabitosDeCompraResposta(IReadOnlyList<MercadoHabitualResposta> Mercados, IReadOnlyList<ResponsavelHabitualResposta> Responsaveis);

public sealed record ItemSugeridoResposta(string Chave, string Nome, string CategoriaNome, string Unidade, decimal Quantidade, int CicloEmDias, int Compras, DateOnly UltimaCompra, DateOnly ProximaPrevista, string Motivo, string Descricao, decimal? PrecoReferencia, Guid? CategoriaId);

public sealed record ModeloParadoResposta(Guid ItemId, string Nome, DateOnly UltimaCompra, int DiasSemComprar, string Descricao);

/// <summary>Repor = o que já está na hora (ou nos próximos dias) pelo ritmo de compra; ListaPadraoParada = itens do modelo sem compra há muito tempo.</summary>
public sealed record AvisosDeRitmoResposta(IReadOnlyList<ItemSugeridoResposta> Repor, IReadOnlyList<ModeloParadoResposta> ListaPadraoParada);

public sealed record SugestaoDeListaResposta(string Competencia, IReadOnlyList<ItemSugeridoResposta> Itens);

public sealed record AnaliseDeGastosResposta(
    IReadOnlyList<GastoDoMesResposta> Meses,
    string? CompetenciaDasCategorias,
    IReadOnlyList<GastoDaCategoriaResposta> Categorias,
    IReadOnlyList<ProdutoFrequenteResposta> Frequentes);

public sealed record PontoDePrecoResposta(string Competencia, string CompetenciaNome, decimal Preco);

public sealed record SerieDePrecoResposta(
    string Nome,
    string UnidadeBase,
    IReadOnlyList<PontoDePrecoResposta> Pontos,
    decimal UltimoPreco,
    decimal MenorPreco,
    decimal MaiorPreco,
    decimal PrecoMedio,
    decimal? VariacaoPercentual);

public sealed record AnaliseDePrecosResposta(IReadOnlyList<SerieDePrecoResposta> Produtos, IReadOnlyList<SerieDePrecoResposta> MaioresAumentos);
