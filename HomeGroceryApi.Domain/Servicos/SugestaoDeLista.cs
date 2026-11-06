using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Servicos;

public enum MotivoDaSugestaoEnum
{
    /// <summary>A próxima compra prevista cai no mês da lista (ou logo depois dele).</summary>
    NoCiclo = 1,

    /// <summary>A próxima compra prevista já passou: o produto está em falta no ritmo habitual.</summary>
    Atrasado = 2,
}

/// <param name="Chave">Identifica o produto (nome sem acento + unidade-base); é o que o app devolve ao criar a lista.</param>
/// <param name="Referencia">Compra mais recente do produto: dela saem categoria, produto e preço de referência.</param>
/// <param name="Quantidade">Mediana do que se leva por dia de compra, na unidade da compra mais recente.</param>
/// <param name="CicloEmDias">Mediana dos intervalos entre dias de compra.</param>
/// <param name="Compras">Quantos dias diferentes o produto foi comprado.</param>
public sealed record ProdutoSugerido(string Chave, ItemDaLista Referencia, decimal Quantidade, int CicloEmDias, int Compras, DateOnly UltimaCompra, DateOnly ProximaPrevista, MotivoDaSugestaoEnum Motivo);

public sealed record ModeloParado(ItemDoModelo Item, DateOnly UltimaCompra, int DiasSemComprar);

/// <summary>
/// Sugere produtos para a lista de um mês a partir do ritmo de compra de cada um.
/// O ritmo vem do DIA em que cada item foi marcado como comprado (não do mês da lista): assim a carne comprada
/// nos dias 1, 10 e 20, em três listas do mesmo mês, vira "a cada ~10 dias" em vez de uma compra só.
/// </summary>
public static class SugestaoDeLista
{
    /// <summary>Folga antes do fim do mês: o arroz de ciclo bimestral já entra na lista do mês anterior à reposição.</summary>
    private const decimal FolgaDoCiclo = 0.25m;

    /// <summary>Produto sem compra há mais de N ciclos é considerado abandonado e não é sugerido.</summary>
    private const int CiclosParaAbandonar = 4;

    /// <summary>Compras gravadas em UTC; o dia que o usuário vê é o do Brasil (UTC-3), senão a compra das 22h cairia no dia seguinte.</summary>
    public static DateOnly DiaDaCompra(DateTime momentoUtc)
    {
        return DateOnly.FromDateTime(momentoUtc.AddHours(-3));
    }

    public static IReadOnlyList<ProdutoSugerido> Calcular(IEnumerable<ItemComCompetencia> itens, Competencia competenciaDaLista, DateOnly hoje)
    {
        var lista = itens.ToList();
        var inicioDoMes = new DateOnly(competenciaDaLista.Ano, competenciaDaLista.Mes, 1);
        var fimDoMes = inicioDoMes.AddMonths(1).AddDays(-1);
        var limiteDoHistorico = hoje < fimDoMes ? hoje : fimDoMes;

        // Quem já está pendente numa lista deste mês não precisa ser sugerido de novo.
        // Já comprado não conta: repor depois de comprar é justamente o caso da carne.
        var pendentesNoMes = lista
            .Where(i => i.Competencia == competenciaDaLista.Chave && !i.Item.Comprado)
            .Select(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot))
            .ToHashSet();

        var resultado = new List<ProdutoSugerido>();
        var grupos = lista
            .Where(i => i.Item.Comprado && i.Item.CompradoEm is not null && DiaDaCompra(i.Item.CompradoEm.Value) <= limiteDoHistorico)
            .GroupBy(i => (Nome: AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot), Base: AnaliseDeCompras.NormalizarUnidade(i.Item.UnidadeSnapshot).Base));

        foreach (var grupo in grupos)
        {
            if (pendentesNoMes.Contains(grupo.Key.Nome)) continue;

            // Vários itens do mesmo produto no mesmo dia são uma compra só.
            var porDia = grupo
                .GroupBy(i => DiaDaCompra(i.Item.CompradoEm!.Value))
                .OrderBy(d => d.Key)
                .ToList();
            if (porDia.Count < 2) continue;

            var dias = porDia.Select(d => d.Key).ToList();
            var intervalos = dias.Zip(dias.Skip(1), (a, b) => b.DayNumber - a.DayNumber).ToList();
            var ciclo = Math.Max(1, (int)Math.Round(Mediana(intervalos.Select(x => (decimal)x).ToList()), MidpointRounding.AwayFromZero));

            var ultima = dias[^1];
            if (hoje.DayNumber - ultima.DayNumber > ciclo * CiclosParaAbandonar) continue;

            var proxima = ultima.AddDays(ciclo);
            var folga = (int)Math.Round(ciclo * FolgaDoCiclo, MidpointRounding.AwayFromZero);
            if (proxima.AddDays(-folga) > fimDoMes) continue;

            var referencia = porDia[^1].OrderByDescending(i => i.Item.CompradoEm).First().Item;
            // O fator de NormalizarUnidade é o do PREÇO (R$/g x 1000 = R$/kg); para quantidade a conta é a inversa (g / 1000 = kg).
            var fatorDaReferencia = AnaliseDeCompras.NormalizarUnidade(referencia.UnidadeSnapshot).Fator;
            var quantidades = porDia
                .Select(d => d.Sum(i => (i.Item.QuantidadeComprada ?? i.Item.QuantidadePlanejada) / AnaliseDeCompras.NormalizarUnidade(i.Item.UnidadeSnapshot).Fator))
                .Select(q => q * fatorDaReferencia)
                .ToList();
            var quantidade = Math.Round(Mediana(quantidades), 3, MidpointRounding.AwayFromZero);
            if (quantidade <= 0) quantidade = 1;

            resultado.Add(new ProdutoSugerido(
                $"{grupo.Key.Nome}|{grupo.Key.Base}", referencia, quantidade, ciclo, porDia.Count, ultima, proxima,
                proxima < hoje ? MotivoDaSugestaoEnum.Atrasado : MotivoDaSugestaoEnum.NoCiclo));
        }

        return resultado
            .OrderBy(s => s.Motivo == MotivoDaSugestaoEnum.Atrasado ? 0 : 1)
            .ThenBy(s => s.ProximaPrevista)
            .ThenBy(s => s.Referencia.NomeSnapshot, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Piso do "parado": passar disso sem comprar um item da lista padrão já merece a pergunta se ele ainda é necessário.</summary>
    private const int DiasParaConsiderarParado = 90;

    /// <summary>
    /// Itens da lista padrão que o grupo parou de comprar. Só entra quem TEM histórico: item nunca comprado não diz
    /// há quanto tempo está parado (o modelo não guarda a data em que entrou). Quem já tem ritmo conhecido ganha folga
    /// proporcional (2 ciclos), para o arroz que se compra a cada 4 meses não ser tratado como esquecido.
    /// </summary>
    public static IReadOnlyList<ModeloParado> ModeloParado(IEnumerable<ItemDoModelo> modelo, IEnumerable<ItemComCompetencia> itens, DateOnly hoje)
    {
        var diasPorProduto = itens
            .Where(i => i.Item.Comprado && i.Item.CompradoEm is not null)
            .GroupBy(i => AnaliseDeCompras.Normalizar(i.Item.NomeSnapshot))
            .ToDictionary(g => g.Key, g => g.Select(i => DiaDaCompra(i.Item.CompradoEm!.Value)).Distinct().Order().ToList());

        var resultado = new List<ModeloParado>();
        foreach (var item in modelo)
        {
            if (!diasPorProduto.TryGetValue(AnaliseDeCompras.Normalizar(item.Nome), out var dias)) continue;

            var intervalos = dias.Zip(dias.Skip(1), (a, b) => (decimal)(b.DayNumber - a.DayNumber)).ToList();
            var limite = intervalos.Count == 0 ? DiasParaConsiderarParado : Math.Max(DiasParaConsiderarParado, (int)Math.Round(Mediana(intervalos) * 2, MidpointRounding.AwayFromZero));
            var semComprar = hoje.DayNumber - dias[^1].DayNumber;
            if (semComprar >= limite) resultado.Add(new ModeloParado(item, dias[^1], semComprar));
        }

        return resultado.OrderByDescending(m => m.DiasSemComprar).ThenBy(m => m.Item.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static decimal Mediana(List<decimal> valores)
    {
        var ordenados = valores.Order().ToList();
        var meio = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2m;
    }
}
