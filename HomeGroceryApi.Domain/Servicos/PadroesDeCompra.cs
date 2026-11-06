using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Servicos;

/// <param name="PercentualAbaixo">Quanto o último preço pago ficou abaixo da média do produto, em %, arredondado.</param>
/// <param name="Meses">Quantos meses diferentes entraram na média.</param>
public sealed record PrecoBom(string Nome, UnidadeDeMedidaEnum UnidadeBase, decimal UltimoPreco, decimal PrecoMedio, int PercentualAbaixo, int Meses);

/// <param name="Anos">Anos anteriores em que o produto foi comprado neste mesmo mês do calendário.</param>
public sealed record ProdutoSazonal(string Chave, ItemDaLista Referencia, IReadOnlyList<int> Anos, DateOnly UltimaCompraNoMes);

/// <param name="IdasDoPrimeiro">Em quantos dias de compra o primeiro produto apareceu (o de menor contagem dos dois).</param>
/// <param name="Idas">Total de dias de compra do grupo.</param>
public sealed record ParCasado(string Primeiro, string Segundo, int Juntos, int IdasDoPrimeiro, int IdasDoSegundo, int Idas, decimal Lift);

/// <summary>
/// Padrões que só aparecem olhando o histórico inteiro: preço abaixo do habitual, produtos de época e produtos que
/// costumam ir juntos para o carrinho. Todos partem de itens comprados, como o resto das análises.
/// </summary>
public static class PadroesDeCompra
{
    /// <summary>Preço só é "bom" com histórico para comparar: ao menos 3 meses e 10% abaixo da média.</summary>
    private const int MesesMinimosDePreco = 3;

    private const decimal QuedaMinimaDePreco = 0.10m;

    /// <summary>Quem comprou algo nos últimos 60 dias não é "de época": já está no ritmo normal.</summary>
    private const int DiasDeCompraRecente = 60;

    private const int IdasMinimasParaCasar = 5;
    private const int JuntosMinimos = 3;
    private const decimal LiftMinimo = 1.5m;
    private const decimal ConfiancaMinima = 0.6m;
    private const int LimiteDeCasados = 20;

    /// <summary>
    /// Produtos cujo último preço pago ficou bem abaixo da média. O app não sabe o preço de hoje na prateleira:
    /// o texto fala da última compra, nunca promete que o preço continua baixo.
    /// </summary>
    public static IReadOnlyList<PrecoBom> PrecosBons(IEnumerable<SerieDePreco> series)
    {
        return series
            .Where(s => s.Pontos.Count >= MesesMinimosDePreco && s.PrecoMedio > 0 && s.UltimoPreco <= s.PrecoMedio * (1 - QuedaMinimaDePreco))
            .Select(s => new PrecoBom(s.Nome, s.UnidadeBase, s.UltimoPreco, s.PrecoMedio, (int)Math.Round((s.PrecoMedio - s.UltimoPreco) / s.PrecoMedio * 100m, MidpointRounding.AwayFromZero), s.Pontos.Count))
            .OrderByDescending(p => p.PercentualAbaixo)
            .ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Produtos comprados neste mesmo mês do calendário em anos anteriores (panetone em dezembro). Ficam de fora os que
    /// já têm ritmo próprio (a sugestão da lista já os traz), os que já estão pendentes na lista do mês e os comprados há pouco.
    /// </summary>
    public static IReadOnlyList<ProdutoSazonal> Sazonais(IEnumerable<ItemComCompetencia> itens, Competencia competenciaDaLista, DateOnly hoje)
    {
        var lista = itens.ToList();
        var comRitmo = SugestaoDeLista.Calcular(lista, competenciaDaLista, hoje).Select(s => s.Chave).ToHashSet();
        var pendentesNoMes = lista
            .Where(i => i.Competencia == competenciaDaLista.Chave && !i.Item.Comprado)
            .Select(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot))
            .ToHashSet();

        var resultado = new List<ProdutoSazonal>();
        var grupos = lista
            .Where(i => i.Item.Comprado && i.Item.CompradoEm is not null)
            .GroupBy(i => (Nome: AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot), Base: AnaliseDeCompras.NormalizarUnidade(i.Item.UnidadeSnapshot).Base));

        foreach (var grupo in grupos)
        {
            var chave = $"{grupo.Key.Nome}|{grupo.Key.Base}";
            if (comRitmo.Contains(chave) || pendentesNoMes.Contains(grupo.Key.Nome)) continue;

            var compras = grupo.Select(i => (Dia: SugestaoDeLista.DiaDaCompra(i.Item.CompradoEm!.Value), i.Item)).ToList();
            if (compras.Any(c => c.Dia <= hoje && hoje.DayNumber - c.Dia.DayNumber <= DiasDeCompraRecente)) continue;

            var noMes = compras.Where(c => c.Dia.Month == competenciaDaLista.Mes && c.Dia.Year < competenciaDaLista.Ano).ToList();
            if (noMes.Count == 0) continue;

            var maisRecente = noMes.OrderByDescending(c => c.Dia).First();
            resultado.Add(new ProdutoSazonal(chave, maisRecente.Item, noMes.Select(c => c.Dia.Year).Distinct().Order().ToList(), maisRecente.Dia));
        }

        return resultado
            .OrderByDescending(s => s.Anos.Count)
            .ThenBy(s => s.Referencia.NomeSnapshot, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pares de produtos que vão juntos para o carrinho mais do que o acaso explicaria. Usa o "lift" sobre os dias de
    /// compra: o que vai em TODA ida ao mercado tem lift 1 com qualquer outro e nunca vira par, só o que realmente se atrai.
    /// </summary>
    public static IReadOnlyList<ParCasado> Casados(IEnumerable<ItemComCompetencia> itens)
    {
        var porDia = itens
            .Where(i => i.Item.Comprado && i.Item.CompradoEm is not null)
            .GroupBy(i => SugestaoDeLista.DiaDaCompra(i.Item.CompradoEm!.Value))
            .ToDictionary(g => g.Key, g => g.Select(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot)).Distinct().ToList());
        if (porDia.Count < IdasMinimasParaCasar) return [];

        var nomes = itens
            .Where(i => i.Item.Comprado && i.Item.CompradoEm is not null)
            .GroupBy(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Item.CompradoEm).First().Item.NomeSnapshot);

        var idasPorProduto = porDia.Values.SelectMany(p => p).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        var juntos = new Dictionary<(string, string), int>();
        foreach (var produtos in porDia.Values)
        {
            var candidatos = produtos.Where(p => idasPorProduto[p] >= JuntosMinimos).Order(StringComparer.Ordinal).ToList();
            for (var i = 0; i < candidatos.Count; i++)
            {
                for (var j = i + 1; j < candidatos.Count; j++)
                {
                    var par = (candidatos[i], candidatos[j]);
                    juntos[par] = juntos.GetValueOrDefault(par) + 1;
                }
            }
        }

        var total = porDia.Count;
        var resultado = new List<ParCasado>();
        foreach (var ((a, b), co) in juntos)
        {
            if (co < JuntosMinimos) continue;
            var idasA = idasPorProduto[a];
            var idasB = idasPorProduto[b];
            var lift = (decimal)co * total / ((decimal)idasA * idasB);
            // Confiança: o produto menos comprado quase sempre vem acompanhado do outro.
            if (lift < LiftMinimo || (decimal)co / Math.Min(idasA, idasB) < ConfiancaMinima) continue;

            // O primeiro do par é o menos comprado: "em N das vezes que comprou X, levou também Y".
            var (primeiro, segundo, idasPrimeiro, idasSegundo) = idasA <= idasB ? (a, b, idasA, idasB) : (b, a, idasB, idasA);
            resultado.Add(new ParCasado(nomes[primeiro], nomes[segundo], co, idasPrimeiro, idasSegundo, total, Math.Round(lift, 2)));
        }

        return resultado
            .OrderByDescending(p => p.Lift)
            .ThenByDescending(p => p.Juntos)
            .ThenBy(p => p.Primeiro, StringComparer.CurrentCultureIgnoreCase)
            .Take(LimiteDeCasados)
            .ToList();
    }
}
