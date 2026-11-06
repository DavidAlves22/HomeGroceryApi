namespace HomeGroceryApi.Domain.Servicos;

/// <param name="Compras">Quantas compras do produto foram no mercado habitual.</param>
/// <param name="Total">Quantas compras do produto têm mercado registrado.</param>
public sealed record MercadoHabitual(string Chave, string Nome, Guid LojaId, int Compras, int Total);

/// <param name="Compras">Quantos itens da categoria essa pessoa marcou como comprados.</param>
/// <param name="Total">Quantos itens da categoria foram marcados por pessoas que ainda são do grupo.</param>
public sealed record ResponsavelHabitual(Guid CategoriaId, string Categoria, Guid UsuarioId, int Compras, int Total);

/// <summary>
/// Hábitos do grupo lidos de quem comprou o quê e onde: o mercado de cada produto e a pessoa de cada categoria.
/// São sugestões para o app mostrar; nada aqui altera listas.
/// </summary>
public static class HabitosDeCompra
{
    /// <summary>Hábito só existe com repetição: ao menos 2 compras com mercado e 60% delas no mesmo lugar.</summary>
    private const int MinimoDeComprasPorProduto = 2;

    private const int MinimoDeComprasPorCategoria = 3;
    private const decimal ParticipacaoMinima = 0.6m;

    public static IReadOnlyList<MercadoHabitual> MercadoPorProduto(IEnumerable<ItemComCompetencia> itens)
    {
        var resultado = new List<MercadoHabitual>();
        var grupos = itens
            .Where(i => i.Item.Comprado && i.Item.CompradoNaLojaId is not null)
            .GroupBy(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot));

        foreach (var grupo in grupos)
        {
            var total = grupo.Count();
            if (total < MinimoDeComprasPorProduto) continue;

            var topo = grupo.GroupBy(i => i.Item.CompradoNaLojaId!.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
            if ((decimal)topo.Count() / total < ParticipacaoMinima) continue;

            resultado.Add(new MercadoHabitual(grupo.Key, grupo.OrderByDescending(i => i.Item.CompradoEm).First().Item.NomeSnapshot, topo.Key, topo.Count(), total));
        }

        return resultado.OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Quem costuma comprar cada categoria. Só conta quem ainda está no grupo, e só sugere quando há mais de uma
    /// pessoa comprando: se uma pessoa faz tudo sozinha, dividir não diria nada.
    /// </summary>
    public static IReadOnlyList<ResponsavelHabitual> ResponsavelPorCategoria(IEnumerable<ItemComCompetencia> itens, IReadOnlySet<Guid> membrosAtuais)
    {
        var resultado = new List<ResponsavelHabitual>();
        var compras = itens
            .Where(i => i.Item.Comprado && i.Item.CategoriaId is not null && i.Item.CompradoPorUsuarioId is { } quem && membrosAtuais.Contains(quem))
            .ToList();
        if (compras.Select(i => i.Item.CompradoPorUsuarioId).Distinct().Count() < 2) return resultado;

        foreach (var grupo in compras.GroupBy(i => i.Item.CategoriaId!.Value))
        {
            var total = grupo.Count();
            if (total < MinimoDeComprasPorCategoria) continue;

            var topo = grupo.GroupBy(i => i.Item.CompradoPorUsuarioId!.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
            if ((decimal)topo.Count() / total < ParticipacaoMinima) continue;

            var maisRecente = grupo.OrderByDescending(i => i.Item.CompradoEm).First().Item;
            resultado.Add(new ResponsavelHabitual(grupo.Key, maisRecente.CategoriaNomeSnapshot, topo.Key, topo.Count(), total));
        }

        return resultado.OrderBy(r => r.Categoria, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
