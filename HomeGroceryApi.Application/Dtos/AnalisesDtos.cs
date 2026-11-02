namespace HomeGroceryApi.Application.Dtos;

public sealed record GastoDoMesResposta(string Competencia, string CompetenciaNome, decimal Estimado, decimal Realizado, int Itens, int Comprados, decimal? VariacaoPercentual);

public sealed record GastoDaCategoriaResposta(string Categoria, decimal Total, decimal Percentual);

public sealed record ProdutoFrequenteResposta(string Nome, int Compras);

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
