using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Servicos;

public sealed record TotaisPorCategoria(string Categoria, decimal Estimado, decimal Realizado, int Itens, int Comprados);

public sealed record TotaisDaLista(
    decimal Estimado,
    decimal Realizado,
    decimal Restante,
    int Itens,
    int Comprados,
    int CompradosSemPreco,
    IReadOnlyList<TotaisPorCategoria> PorCategoria);

/// <summary>
/// Regras financeiras da lista. Valores em `decimal` (nunca double) e cada linha é arredondada ao centavo
/// ANTES de somar, para a soma da tela bater com o que está no cupom (6 x R$ 5,50 = R$ 33,00 exatos).
/// </summary>
public static class CalculoDeTotais
{
    public static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Quantidade planejada x preço estimado. Sem preço estimado, o item não entra na estimativa.</summary>
    public static decimal TotalEstimadoDoItem(ItemDaLista item)
    {
        return item.PrecoEstimado is { } preco ? Arredondar(item.QuantidadePlanejada * preco) : 0m;
    }

    /// <summary>
    /// Quantidade comprada (ou a planejada, se não informada) x preço pago. Só conta item COMPRADO e com
    /// preço pago informado: um preço de referência nunca vira gasto automaticamente.
    /// </summary>
    public static decimal TotalRealizadoDoItem(ItemDaLista item)
    {
        if (!item.Comprado || item.PrecoUnitario is not { } preco) return 0m;
        return Arredondar((item.QuantidadeComprada ?? item.QuantidadePlanejada) * preco);
    }

    public static TotaisDaLista Calcular(IReadOnlyCollection<ItemDaLista> itens)
    {
        var estimado = itens.Sum(TotalEstimadoDoItem);
        var realizado = itens.Sum(TotalRealizadoDoItem);

        var porCategoria = itens
            .GroupBy(i => i.CategoriaNomeSnapshot)
            .Select(g => new TotaisPorCategoria(g.Key, g.Sum(TotalEstimadoDoItem), g.Sum(TotalRealizadoDoItem), g.Count(), g.Count(i => i.Comprado)))
            .OrderByDescending(c => c.Realizado)
            .ThenBy(c => c.Categoria, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new TotaisDaLista(
            estimado, realizado, estimado - realizado, itens.Count, itens.Count(i => i.Comprado),
            itens.Count(i => i.Comprado && i.PrecoUnitario is null), porCategoria);
    }
}
