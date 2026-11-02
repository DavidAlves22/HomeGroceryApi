using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class CalculoDeTotaisTests
{
    private static readonly Guid Lista = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();

    internal static ItemDaLista Item(string nome, string categoria, decimal quantidade, decimal? precoEstimado = null, decimal? precoPago = null, bool comprado = false, decimal? quantidadeComprada = null, UnidadeDeMedidaEnum unidade = UnidadeDeMedidaEnum.Unidade, Guid? lista = null)
    {
        var item = ItemDaLista.Criar(Guid.NewGuid(), lista ?? Lista, Guid.NewGuid(), null, nome, null, categoria, unidade, quantidade, 0, Usuario, precoEstimado);
        item.Editar(Guid.NewGuid(), nome, Guid.NewGuid(), categoria, unidade, quantidade, quantidadeComprada, precoEstimado, precoPago);
        item.DefinirComprado(comprado, Usuario, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        return item;
    }

    [Fact]
    public void Exemplo_da_especificacao_soma_101_reais()
    {
        var itens = new[]
        {
            Item("Arroz", "Mercearia", 2, 25m, 25m, true),
            Item("Leite", "Laticínios", 6, 5.50m, 5.50m, true),
            Item("Café", "Mercearia", 1, 18m, 18m, true),
        };

        var totais = CalculoDeTotais.Calcular(itens);

        Assert.Equal(101.00m, totais.Realizado);
        Assert.Equal(101.00m, totais.Estimado);
        Assert.Equal(0m, totais.Restante);
        Assert.Equal(3, totais.Comprados);
    }

    [Fact]
    public void Estimado_e_realizado_sao_independentes_e_o_restante_e_a_diferenca()
    {
        var itens = new[]
        {
            Item("Arroz", "Mercearia", 2, 25m, 24m, true),
            Item("Leite", "Laticínios", 6, 5m),
        };

        var totais = CalculoDeTotais.Calcular(itens);

        Assert.Equal(80m, totais.Estimado);
        Assert.Equal(48m, totais.Realizado);
        Assert.Equal(32m, totais.Restante);
    }

    [Fact]
    public void Preco_pago_so_conta_quando_o_item_foi_comprado()
    {
        var pendente = Item("Arroz", "Mercearia", 2, null, 25m, false);

        Assert.Equal(0m, CalculoDeTotais.TotalRealizadoDoItem(pendente));
    }

    [Fact]
    public void Preco_estimado_nunca_vira_gasto_automaticamente()
    {
        var comprado = Item("Arroz", "Mercearia", 2, 25m, null, true);

        var totais = CalculoDeTotais.Calcular([comprado]);

        Assert.Equal(0m, totais.Realizado);
        Assert.Equal(50m, totais.Estimado);
        Assert.Equal(1, totais.CompradosSemPreco);
    }

    [Fact]
    public void Quantidade_comprada_substitui_a_planejada_no_total_gasto()
    {
        var item = Item("Carne", "Açougue", 1.5m, 38m, 40m, true, quantidadeComprada: 1.2m);

        Assert.Equal(48.00m, CalculoDeTotais.TotalRealizadoDoItem(item));
        Assert.Equal(57.00m, CalculoDeTotais.TotalEstimadoDoItem(item));
    }

    [Theory]
    [InlineData(0.333, 9.99, 3.33)]
    [InlineData(0.5, 0.01, 0.01)]
    [InlineData(3, 0.335, 1.01)]
    [InlineData(6, 5.5, 33.00)]
    public void Cada_linha_e_arredondada_ao_centavo_antes_de_somar(double quantidade, double preco, double esperado)
    {
        var item = Item("X", "Y", (decimal)quantidade, null, (decimal)preco, true);

        Assert.Equal((decimal)esperado, CalculoDeTotais.TotalRealizadoDoItem(item));
    }

    [Fact]
    public void Soma_de_muitos_centavos_nao_acumula_erro()
    {
        var itens = Enumerable.Range(0, 100).Select(_ => Item("Bala", "Doces", 1, null, 0.10m, true)).ToList();

        Assert.Equal(10.00m, CalculoDeTotais.Calcular(itens).Realizado);
    }

    [Fact]
    public void Totais_por_categoria_ficam_em_ordem_do_maior_gasto()
    {
        var itens = new[]
        {
            Item("Arroz", "Mercearia", 1, null, 10m, true),
            Item("Carne", "Açougue", 1, null, 50m, true),
            Item("Café", "Mercearia", 1, null, 15m, true),
        };

        var categorias = CalculoDeTotais.Calcular(itens).PorCategoria;

        Assert.Equal(["Açougue", "Mercearia"], categorias.Select(c => c.Categoria).ToArray());
        Assert.Equal(25m, categorias[1].Realizado);
    }
}

public class AnaliseDeComprasTests
{
    private static readonly Dictionary<string, Guid> ListasPorMes = [];

    private static Guid ListaDoMes(string competencia)
    {
        if (!ListasPorMes.TryGetValue(competencia, out var id)) ListasPorMes[competencia] = id = Guid.NewGuid();
        return id;
    }

    private static ItemComCompetencia Compra(string competencia, string nome, string categoria, decimal preco, decimal quantidade = 1, UnidadeDeMedidaEnum unidade = UnidadeDeMedidaEnum.Unidade, bool comprado = true)
    {
        // Cada mês é uma lista diferente, como no app de verdade.
        return new ItemComCompetencia(competencia, CalculoDeTotaisTests.Item(nome, categoria, quantidade, null, preco, comprado, unidade: unidade, lista: ListaDoMes(competencia)));
    }

    [Fact]
    public void Gasto_por_mes_calcula_a_variacao_em_relacao_ao_mes_anterior()
    {
        var itens = new[]
        {
            Compra("2026-09", "Arroz", "Mercearia", 100m),
            Compra("2026-10", "Arroz", "Mercearia", 120m),
            Compra("2026-11", "Arroz", "Mercearia", 90m),
        };

        var meses = AnaliseDeCompras.GastoPorMes(itens);

        Assert.Equal(["2026-09", "2026-10", "2026-11"], meses.Select(m => m.Competencia).ToArray());
        Assert.Null(meses[0].VariacaoPercentual);
        Assert.Equal(20.0m, meses[1].VariacaoPercentual);
        Assert.Equal(-25.0m, meses[2].VariacaoPercentual);
    }

    [Fact]
    public void Gasto_por_categoria_calcula_o_percentual_do_total()
    {
        var itens = new[]
        {
            Compra("2026-11", "Arroz", "Mercearia", 75m),
            Compra("2026-11", "Leite", "Laticínios", 25m),
            Compra("2026-10", "Carne", "Açougue", 500m),
        };

        var categorias = AnaliseDeCompras.GastoPorCategoria(itens, "2026-11");

        Assert.Equal(2, categorias.Count);
        Assert.Equal("Mercearia", categorias[0].Categoria);
        Assert.Equal(75.0m, categorias[0].Percentual);
        Assert.Equal(25.0m, categorias[1].Percentual);
    }

    [Fact]
    public void Serie_de_preco_traz_ultimo_menor_maior_medio_e_variacao()
    {
        var itens = new[]
        {
            Compra("2026-09", "Café", "Mercearia", 16m),
            Compra("2026-10", "Café", "Mercearia", 18m),
            Compra("2026-11", "café ", "Mercearia", 20m),
        };

        var serie = Assert.Single(AnaliseDeCompras.SeriesDePreco(itens));

        Assert.Equal(20m, serie.UltimoPreco);
        Assert.Equal(16m, serie.MenorPreco);
        Assert.Equal(20m, serie.MaiorPreco);
        Assert.Equal(18m, serie.PrecoMedio);
        Assert.Equal(11.1m, serie.VariacaoPercentual);
        Assert.Equal(3, serie.Pontos.Count);
    }

    [Fact]
    public void Preco_por_grama_e_por_quilo_sao_comparados_na_mesma_base()
    {
        var itens = new[]
        {
            Compra("2026-10", "Queijo", "Laticínios", 40m, unidade: UnidadeDeMedidaEnum.Kg),
            Compra("2026-11", "Queijo", "Laticínios", 0.05m, unidade: UnidadeDeMedidaEnum.G),
        };

        var serie = Assert.Single(AnaliseDeCompras.SeriesDePreco(itens));

        Assert.Equal(UnidadeDeMedidaEnum.Kg, serie.UnidadeBase);
        Assert.Equal(50m, serie.UltimoPreco);
        Assert.Equal(25.0m, serie.VariacaoPercentual);
    }

    [Fact]
    public void Unidades_incompativeis_formam_series_separadas()
    {
        var itens = new[]
        {
            Compra("2026-10", "Arroz", "Mercearia", 25m, unidade: UnidadeDeMedidaEnum.Pacote),
            Compra("2026-11", "Arroz", "Mercearia", 6m, unidade: UnidadeDeMedidaEnum.Kg),
        };

        Assert.Equal(2, AnaliseDeCompras.SeriesDePreco(itens).Count);
    }

    [Fact]
    public void Itens_pendentes_ou_sem_preco_ficam_fora_do_historico_de_precos()
    {
        var itens = new[]
        {
            Compra("2026-11", "Arroz", "Mercearia", 25m, comprado: false),
            new ItemComCompetencia("2026-11", CalculoDeTotaisTests.Item("Leite", "Laticínios", 1, 5m, null, true)),
        };

        Assert.Empty(AnaliseDeCompras.SeriesDePreco(itens));
    }

    [Fact]
    public void Maiores_aumentos_ordena_pela_variacao_e_ignora_quedas()
    {
        var itens = new[]
        {
            Compra("2026-10", "Café", "M", 10m), Compra("2026-11", "Café", "M", 15m),
            Compra("2026-10", "Leite", "L", 5m), Compra("2026-11", "Leite", "L", 5.5m),
            Compra("2026-10", "Arroz", "M", 30m), Compra("2026-11", "Arroz", "M", 20m),
        };

        var aumentos = AnaliseDeCompras.MaioresAumentos(AnaliseDeCompras.SeriesDePreco(itens), 5);

        Assert.Equal(["Café", "Leite"], aumentos.Select(a => a.Nome).ToArray());
    }

    [Fact]
    public void Produtos_frequentes_contam_listas_diferentes_ignorando_acento_e_caixa()
    {
        var itens = new[]
        {
            Compra("2026-09", "Café", "M", 10m), Compra("2026-10", "cafe", "M", 11m), Compra("2026-11", "CAFÉ", "M", 12m),
            Compra("2026-11", "Arroz", "M", 25m),
        };

        var frequentes = AnaliseDeCompras.Frequentes(itens, 5);

        Assert.Equal("café", frequentes[0].Nome.ToLowerInvariant());
        Assert.Equal(3, frequentes[0].Compras);
        Assert.Equal(1, frequentes[1].Compras);
    }
}
