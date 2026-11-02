using System.Globalization;
using System.Text;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Servicos;

/// <summary>Um item de lista junto com o mês dela (base de todas as análises).</summary>
public sealed record ItemComCompetencia(string Competencia, ItemDaLista Item);

public sealed record GastoDoMes(string Competencia, decimal Estimado, decimal Realizado, int Itens, int Comprados, decimal? VariacaoPercentual);

public sealed record GastoDaCategoria(string Categoria, decimal Total, decimal Percentual);

public sealed record ProdutoFrequente(string Nome, int Compras);

public sealed record PontoDePreco(string Competencia, decimal Preco);

public sealed record SerieDePreco(
    string Nome,
    UnidadeDeMedidaEnum UnidadeBase,
    IReadOnlyList<PontoDePreco> Pontos,
    decimal UltimoPreco,
    decimal MenorPreco,
    decimal MaiorPreco,
    decimal PrecoMedio,
    decimal? VariacaoPercentual);

/// <summary>
/// Análises sobre o histórico de compras. Tudo é calculado a partir dos itens das listas (que já guardam
/// nome, unidade e preço do dia da compra), então o resultado não depende do cadastro atual de produtos.
/// </summary>
public static class AnaliseDeCompras
{
    /// <summary>Gasto de cada mês e quanto variou em relação ao mês anterior da série.</summary>
    public static IReadOnlyList<GastoDoMes> GastoPorMes(IEnumerable<ItemComCompetencia> itens)
    {
        var meses = itens
            .GroupBy(i => i.Competencia)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Competencia: g.Key, Itens: g.Select(x => x.Item).ToList()))
            .ToList();

        var resultado = new List<GastoDoMes>();
        decimal? anterior = null;
        foreach (var (competencia, lista) in meses)
        {
            var realizado = lista.Sum(CalculoDeTotais.TotalRealizadoDoItem);
            var variacao = anterior is > 0 ? Math.Round((realizado - anterior.Value) / anterior.Value * 100m, 1, MidpointRounding.AwayFromZero) : (decimal?)null;
            resultado.Add(new GastoDoMes(competencia, lista.Sum(CalculoDeTotais.TotalEstimadoDoItem), realizado, lista.Count, lista.Count(i => i.Comprado), variacao));
            anterior = realizado;
        }

        return resultado;
    }

    /// <summary>Quanto cada categoria pesou no gasto de um mês (do maior para o menor).</summary>
    public static IReadOnlyList<GastoDaCategoria> GastoPorCategoria(IEnumerable<ItemComCompetencia> itens, string competencia)
    {
        var totais = itens
            .Where(i => i.Competencia == competencia)
            .GroupBy(i => i.Item.CategoriaNomeSnapshot)
            .Select(g => (Categoria: g.Key, Total: g.Sum(x => CalculoDeTotais.TotalRealizadoDoItem(x.Item))))
            .Where(c => c.Total > 0)
            .OrderByDescending(c => c.Total)
            .ToList();

        var soma = totais.Sum(c => c.Total);
        return totais.Select(c => new GastoDaCategoria(c.Categoria, c.Total, soma == 0 ? 0 : Math.Round(c.Total / soma * 100m, 1, MidpointRounding.AwayFromZero))).ToList();
    }

    /// <summary>Produtos que mais aparecem comprados, contando em quantas listas diferentes.</summary>
    public static IReadOnlyList<ProdutoFrequente> Frequentes(IEnumerable<ItemComCompetencia> itens, int limite)
    {
        return itens
            .Where(i => i.Item.Comprado)
            .GroupBy(i => Normalizar(i.Item.NomeSnapshot))
            .Select(g => new ProdutoFrequente(g.OrderByDescending(x => x.Competencia, StringComparer.Ordinal).First().Item.NomeSnapshot, g.Select(x => x.Item.ListaId).Distinct().Count()))
            .OrderByDescending(p => p.Compras)
            .ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Take(limite)
            .ToList();
    }

    /// <summary>
    /// Evolução do preço pago por produto. Preços de unidades compatíveis são comparados na mesma base
    /// (g viram kg, ml viram litro); unidades diferentes (pacote x kg) formam séries separadas, porque
    /// comparar preço de um pacote com preço de um quilo não faria sentido.
    /// </summary>
    public static IReadOnlyList<SerieDePreco> SeriesDePreco(IEnumerable<ItemComCompetencia> itens)
    {
        var compras = itens
            .Where(i => i.Item.Comprado && i.Item.PrecoUnitario is > 0)
            .Select(i =>
            {
                var (unidadeBase, fator) = NormalizarUnidade(i.Item.UnidadeSnapshot);
                return (i.Competencia, i.Item, Base: unidadeBase, Preco: i.Item.PrecoUnitario!.Value * fator);
            })
            .ToList();

        return compras
            .GroupBy(c => (Normalizar(c.Item.NomeSnapshot), c.Base))
            .Select(g =>
            {
                var cronologico = g.OrderBy(c => c.Competencia, StringComparer.Ordinal).ThenBy(c => c.Item.CompradoEm).ToList();
                var pontos = cronologico
                    .GroupBy(c => c.Competencia)
                    .Select(m => new PontoDePreco(m.Key, CalculoDeTotais.Arredondar(m.Average(c => c.Preco))))
                    .ToList();
                var precos = cronologico.Select(c => c.Preco).ToList();
                var variacao = pontos.Count >= 2 && pontos[^2].Preco > 0
                    ? Math.Round((pontos[^1].Preco - pontos[^2].Preco) / pontos[^2].Preco * 100m, 1, MidpointRounding.AwayFromZero)
                    : (decimal?)null;

                return new SerieDePreco(
                    cronologico[^1].Item.NomeSnapshot, g.Key.Base, pontos, CalculoDeTotais.Arredondar(precos[^1]),
                    CalculoDeTotais.Arredondar(precos.Min()), CalculoDeTotais.Arredondar(precos.Max()), CalculoDeTotais.Arredondar(precos.Average()), variacao);
            })
            .OrderBy(s => s.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Produtos cujo preço mais subiu do penúltimo para o último mês comprado.</summary>
    public static IReadOnlyList<SerieDePreco> MaioresAumentos(IEnumerable<SerieDePreco> series, int limite)
    {
        return series.Where(s => s.VariacaoPercentual is > 0).OrderByDescending(s => s.VariacaoPercentual).Take(limite).ToList();
    }

    public static (UnidadeDeMedidaEnum Base, decimal Fator) NormalizarUnidade(UnidadeDeMedidaEnum unidade)
    {
        return unidade switch
        {
            UnidadeDeMedidaEnum.G => (UnidadeDeMedidaEnum.Kg, 1000m),
            UnidadeDeMedidaEnum.Ml => (UnidadeDeMedidaEnum.Litro, 1000m),
            _ => (unidade, 1m),
        };
    }

    /// <summary>Minúsculas e sem acento: "Café" e "cafe" são o mesmo produto.</summary>
    public static string Normalizar(string texto)
    {
        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var semAcento = new string(decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return semAcento.ToLowerInvariant();
    }
}
