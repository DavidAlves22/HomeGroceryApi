using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.UseCases.Listas;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Analises;

/// <summary>
/// Gasto mês a mês (com variação), peso de cada categoria em um mês e produtos mais comprados.
/// </summary>
public sealed class ObterAnaliseDeGastos
{
    private const int MaximoDeMeses = 24;

    private readonly IListaRepository _listas;
    private readonly IUsuarioAtual _usuario;

    public ObterAnaliseDeGastos(IListaRepository listas, IUsuarioAtual usuario)
    {
        _listas = listas;
        _usuario = usuario;
    }

    public async Task<AnaliseDeGastosResposta> ExecutarAsync(int meses, string? competenciaDasCategorias, CancellationToken ct)
    {
        var itens = await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct);
        var gastos = AnaliseDeCompras.GastoPorMes(itens);
        var recentes = gastos.TakeLast(Math.Clamp(meses, 1, MaximoDeMeses)).ToList();

        // Sem mês pedido, mostra as categorias do mês mais recente que já tem gasto registrado.
        var competencia = competenciaDasCategorias is null
            ? recentes.LastOrDefault(g => g.Realizado > 0)?.Competencia ?? recentes.LastOrDefault()?.Competencia
            : Competencia.Parse(competenciaDasCategorias).Chave;

        var categorias = competencia is null ? [] : AnaliseDeCompras.GastoPorCategoria(itens, competencia);

        return new AnaliseDeGastosResposta(
            recentes.Select(g => new GastoDoMesResposta(g.Competencia, Competencia.Parse(g.Competencia).Nome, g.Estimado, g.Realizado, g.Itens, g.Comprados, g.VariacaoPercentual)).ToList(),
            competencia,
            categorias.Select(c => new GastoDaCategoriaResposta(c.Categoria, c.Total, c.Percentual)).ToList(),
            AnaliseDeCompras.Frequentes(itens, 8).Select(f => new ProdutoFrequenteResposta(f.Nome, f.Compras)).ToList());
    }
}

/// <summary>
/// Histórico de preços por produto (último, menor, maior, médio, evolução) e os que mais subiram.
/// </summary>
public sealed class ObterAnaliseDePrecos
{
    private readonly IListaRepository _listas;
    private readonly IUsuarioAtual _usuario;

    public ObterAnaliseDePrecos(IListaRepository listas, IUsuarioAtual usuario)
    {
        _listas = listas;
        _usuario = usuario;
    }

    public async Task<AnaliseDePrecosResposta> ExecutarAsync(CancellationToken ct)
    {
        var itens = await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct);
        var series = AnaliseDeCompras.SeriesDePreco(itens);

        return new AnaliseDePrecosResposta(
            series.Select(ParaResposta).ToList(),
            AnaliseDeCompras.MaioresAumentos(series, 5).Select(ParaResposta).ToList());
    }

    private static SerieDePrecoResposta ParaResposta(SerieDePreco s)
    {
        return new SerieDePrecoResposta(
            s.Nome, s.UnidadeBase.ToString(),
            s.Pontos.Select(p => new PontoDePrecoResposta(p.Competencia, Competencia.Parse(p.Competencia).Nome, p.Preco)).ToList(),
            s.UltimoPreco, s.MenorPreco, s.MaiorPreco, s.PrecoMedio, s.VariacaoPercentual);
    }
}

/// <summary>
/// Avisos do Início e da lista padrão, todos derivados do mesmo ritmo de compra da sugestão inteligente.
/// "Repor" olha o mês atual e inclui o que vence nos próximos 7 dias; quem já está pendente na lista do mês não aparece.
/// </summary>
public sealed class ObterAvisosDeRitmo
{
    private const int DiasDeAntecedencia = 7;

    private readonly IListaRepository _listas;
    private readonly IModeloRepository _modelo;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public ObterAvisosDeRitmo(IListaRepository listas, IModeloRepository modelo, IUsuarioAtual usuario, IRelogio relogio)
    {
        _listas = listas;
        _modelo = modelo;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task<AvisosDeRitmoResposta> ExecutarAsync(CancellationToken ct)
    {
        var hoje = SugestaoDeLista.DiaDaCompra(_relogio.AgoraUtc);
        var itens = await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct);
        var mesAtual = Competencia.Parse($"{hoje.Year:0000}-{hoje.Month:00}");

        var repor = SugestaoDeLista.Calcular(itens, mesAtual, hoje)
            .Where(s => s.ProximaPrevista <= hoje.AddDays(DiasDeAntecedencia))
            .Select(s => ObterSugestaoDeLista.ParaResposta(s, hoje))
            .ToList();

        var parados = SugestaoDeLista.ModeloParado(await _modelo.ListarAsync(_usuario.GrupoId, null, ct), itens, hoje)
            .Select(m => new ModeloParadoResposta(m.Item.Id, m.Item.Nome, m.UltimaCompra, m.DiasSemComprar, $"Última compra em {m.UltimaCompra:dd/MM/yyyy}, há {Descrever(m.DiasSemComprar)}."))
            .ToList();

        return new AvisosDeRitmoResposta(repor, parados);
    }

    private static string Descrever(int dias)
    {
        if (dias < 60) return $"{dias} dias";
        var meses = (int)Math.Round(dias / 30.4, MidpointRounding.AwayFromZero);
        return $"{meses} meses";
    }
}

/// <summary>
/// Padrões do histórico inteiro (ver <see cref="PadroesDeCompra"/>): preço bom na última compra, produtos de época
/// (olhando o mês atual) e pares que costumam ir juntos. Texto de cada achado já vai pronto, em português.
/// </summary>
public sealed class ObterPadroesDeCompra
{
    private readonly IListaRepository _listas;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public ObterPadroesDeCompra(IListaRepository listas, IUsuarioAtual usuario, IRelogio relogio)
    {
        _listas = listas;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task<PadroesDeCompraResposta> ExecutarAsync(CancellationToken ct)
    {
        var hoje = SugestaoDeLista.DiaDaCompra(_relogio.AgoraUtc);
        var itens = await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct);
        var mesAtual = Competencia.Parse($"{hoje.Year:0000}-{hoje.Month:00}");

        var precos = PadroesDeCompra.PrecosBons(AnaliseDeCompras.SeriesDePreco(itens))
            .Select(p => new PrecoBomResposta(p.Nome, p.UnidadeBase.ToString(), p.UltimoPreco, p.PrecoMedio, p.PercentualAbaixo, p.Meses, $"Na última compra você pagou {Reais(p.UltimoPreco)}, {p.PercentualAbaixo}% abaixo da média ({Reais(p.PrecoMedio)}) de {p.Meses} meses."))
            .ToList();

        var sazonais = PadroesDeCompra.Sazonais(itens, mesAtual, hoje)
            .Select(s => new ProdutoSazonalResposta(s.Chave, s.Referencia.NomeSnapshot, s.Referencia.CategoriaNomeSnapshot, s.Referencia.UnidadeSnapshot.ToString(), s.Anos, s.UltimaCompraNoMes, $"Você comprou em {Competencia.NomeDoMes(hoje.Month)} de {string.Join(" e ", s.Anos)}. Última vez em {s.UltimaCompraNoMes:dd/MM/yyyy}.", s.Referencia.CategoriaId))
            .ToList();

        var casados = PadroesDeCompra.Casados(itens)
            .Select(c => new ParCasadoResposta(c.Primeiro, c.Segundo, c.Juntos, $"Em {c.Juntos} das {c.IdasDoPrimeiro} vezes em que você comprou {c.Primeiro}, levou também {c.Segundo}."))
            .ToList();

        return new PadroesDeCompraResposta(precos, sazonais, casados);
    }

    /// <summary>Formatação própria: o contêiner pode rodar sem os dados de cultura, e aí "C" sairia com símbolo errado.</summary>
    private static string Reais(decimal valor)
    {
        return "R$ " + valor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}

/// <summary>
/// Mercado habitual de cada produto e responsável habitual de cada categoria (ver <see cref="HabitosDeCompra"/>).
/// Mercado excluído não é sugerido: o hábito antigo apontaria para um lugar que não existe mais.
/// </summary>
public sealed class ObterHabitosDeCompra
{
    private readonly IListaRepository _listas;
    private readonly ILojaRepository _lojas;
    private readonly IGrupoRepository _grupos;
    private readonly IUsuarioAtual _usuario;

    public ObterHabitosDeCompra(IListaRepository listas, ILojaRepository lojas, IGrupoRepository grupos, IUsuarioAtual usuario)
    {
        _listas = listas;
        _lojas = lojas;
        _grupos = grupos;
        _usuario = usuario;
    }

    public async Task<HabitosDeCompraResposta> ExecutarAsync(CancellationToken ct)
    {
        var itens = await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct);
        var lojasAtivas = (await _lojas.ListarAsync(_usuario.GrupoId, false, ct)).Select(l => l.Id).ToHashSet();
        var membros = (await _grupos.ListarMembrosAsync(_usuario.GrupoId, ct)).Select(m => m.UsuarioId).ToHashSet();

        return new HabitosDeCompraResposta(
            HabitosDeCompra.MercadoPorProduto(itens).Where(m => lojasAtivas.Contains(m.LojaId)).Select(m => new MercadoHabitualResposta(m.Chave, m.Nome, m.LojaId, m.Compras, m.Total)).ToList(),
            HabitosDeCompra.ResponsavelPorCategoria(itens, membros).Select(r => new ResponsavelHabitualResposta(r.CategoriaId, r.Categoria, r.UsuarioId, r.Compras, r.Total)).ToList());
    }
}
