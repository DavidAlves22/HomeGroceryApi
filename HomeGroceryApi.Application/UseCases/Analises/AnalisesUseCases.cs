using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
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
