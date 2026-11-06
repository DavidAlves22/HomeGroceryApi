using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class SugestaoDeListaTests
{
    private static readonly Guid Usuario = Guid.NewGuid();

    /// <summary>Item comprado ao meio-dia (Brasil) do dia informado, numa lista do mês indicado.</summary>
    private static ItemComCompetencia Comprado(string nome, string competencia, DateOnly dia, decimal quantidade = 1, UnidadeDeMedidaEnum unidade = UnidadeDeMedidaEnum.Unidade)
    {
        var item = ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nome, Guid.NewGuid(), "Mercearia", unidade, quantidade, 0, Usuario);
        item.DefinirComprado(true, Usuario, dia.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc));
        return new ItemComCompetencia(competencia, item);
    }

    private static ItemComCompetencia Pendente(string nome, string competencia)
    {
        return new ItemComCompetencia(competencia, ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, nome, null, "Mercearia", UnidadeDeMedidaEnum.Unidade, 1, 0, Usuario));
    }

    private static IReadOnlyList<ProdutoSugerido> Sugerir(IEnumerable<ItemComCompetencia> itens, string competencia, DateOnly hoje) => SugestaoDeLista.Calcular(itens, Competencia.Parse(competencia), hoje);

    [Fact]
    public void Arroz_comprado_em_julho_e_setembro_entra_na_lista_de_outubro()
    {
        var itens = new[] { Comprado("Arroz", "2026-07", new DateOnly(2026, 7, 10)), Comprado("Arroz", "2026-09", new DateOnly(2026, 9, 10)) };

        var sugestoes = Sugerir(itens, "2026-10", new DateOnly(2026, 9, 28));

        var arroz = Assert.Single(sugestoes);
        Assert.Equal(62, arroz.CicloEmDias);
        Assert.Equal(new DateOnly(2026, 11, 11), arroz.ProximaPrevista);
        Assert.Equal(MotivoDaSugestaoEnum.NoCiclo, arroz.Motivo);
    }

    [Fact]
    public void Arroz_de_ciclo_bimestral_ainda_nao_entra_na_lista_do_mes_seguinte_a_compra()
    {
        var itens = new[] { Comprado("Arroz", "2026-07", new DateOnly(2026, 7, 10)), Comprado("Arroz", "2026-09", new DateOnly(2026, 9, 10)) };

        Assert.Empty(Sugerir(itens, "2026-09", new DateOnly(2026, 9, 10)));
    }

    [Fact]
    public void Carne_comprada_nos_dias_1_10_e_20_do_mesmo_mes_tem_ciclo_de_dez_dias()
    {
        var itens = new[]
        {
            Comprado("Carne", "2026-10", new DateOnly(2026, 10, 1), 2, UnidadeDeMedidaEnum.Kg),
            Comprado("Carne", "2026-10", new DateOnly(2026, 10, 10), 1.5m, UnidadeDeMedidaEnum.Kg),
            Comprado("Carne", "2026-10", new DateOnly(2026, 10, 20), 2, UnidadeDeMedidaEnum.Kg),
        };

        var carne = Assert.Single(Sugerir(itens, "2026-11", new DateOnly(2026, 10, 25)));

        Assert.Equal(10, carne.CicloEmDias);
        Assert.Equal(3, carne.Compras);
        Assert.Equal(2m, carne.Quantidade);
        Assert.Equal(new DateOnly(2026, 10, 30), carne.ProximaPrevista);
    }

    [Fact]
    public void Reposicao_dentro_do_mesmo_mes_sugere_a_carne_para_a_lista_extra()
    {
        var itens = new[] { Comprado("Carne", "2026-10", new DateOnly(2026, 10, 1)), Comprado("Carne", "2026-10", new DateOnly(2026, 10, 10)) };

        var carne = Assert.Single(Sugerir(itens, "2026-10", new DateOnly(2026, 10, 21)));

        Assert.Equal(MotivoDaSugestaoEnum.Atrasado, carne.Motivo);
    }

    [Fact]
    public void Produto_ja_pendente_na_lista_do_mes_nao_e_sugerido_mas_o_ja_comprado_nao_impede()
    {
        var historico = new[] { Comprado("Arroz", "2026-08", new DateOnly(2026, 8, 5)), Comprado("Arroz", "2026-09", new DateOnly(2026, 9, 5)), Comprado("Leite", "2026-08", new DateOnly(2026, 8, 5)), Comprado("Leite", "2026-09", new DateOnly(2026, 9, 5)) };

        var comArrozPendente = historico.Append(Pendente("arroz", "2026-10"));
        var comLeiteComprado = historico.Append(Comprado("Leite", "2026-10", new DateOnly(2026, 10, 1)));

        Assert.DoesNotContain(Sugerir(comArrozPendente, "2026-10", new DateOnly(2026, 10, 2)), s => s.Chave.StartsWith("arroz"));
        Assert.Contains(Sugerir(comLeiteComprado, "2026-10", new DateOnly(2026, 10, 2)), s => s.Chave.StartsWith("leite"));
    }

    [Fact]
    public void Produto_comprado_uma_vez_so_nao_tem_ritmo_e_nao_e_sugerido()
    {
        Assert.Empty(Sugerir([Comprado("Chocolate", "2026-09", new DateOnly(2026, 9, 5))], "2026-10", new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Itens_do_mesmo_produto_no_mesmo_dia_sao_uma_compra_so()
    {
        var itens = new[] { Comprado("Leite", "2026-09", new DateOnly(2026, 9, 5), 3), Comprado("leite", "2026-09", new DateOnly(2026, 9, 5), 3) };

        Assert.Empty(Sugerir(itens, "2026-10", new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Produto_sem_compra_ha_mais_de_quatro_ciclos_e_considerado_abandonado()
    {
        var itens = new[] { Comprado("Panetone", "2025-11", new DateOnly(2025, 11, 1)), Comprado("Panetone", "2025-12", new DateOnly(2025, 12, 1)) };

        Assert.Empty(Sugerir(itens, "2026-10", new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Atrasado_vem_antes_do_que_esta_no_ciclo_e_a_quantidade_converte_a_unidade()
    {
        var itens = new[]
        {
            Comprado("Café", "2026-07", new DateOnly(2026, 7, 1), 500, UnidadeDeMedidaEnum.G),
            Comprado("Café", "2026-08", new DateOnly(2026, 8, 1), 1, UnidadeDeMedidaEnum.Kg),
            Comprado("Sabão", "2026-09", new DateOnly(2026, 9, 1)),
            Comprado("Sabão", "2026-10", new DateOnly(2026, 10, 1)),
        };

        var sugestoes = Sugerir(itens, "2026-11", new DateOnly(2026, 10, 20));

        Assert.Equal(["Café", "Sabão"], sugestoes.Select(s => s.Referencia.NomeSnapshot));
        Assert.Equal(MotivoDaSugestaoEnum.Atrasado, sugestoes[0].Motivo);
        Assert.Equal(0.75m, sugestoes[0].Quantidade);
    }

    [Fact]
    public void Compra_noturna_conta_no_dia_do_brasil()
    {
        // 01:00 UTC do dia 11 = 22:00 do dia 10 em Brasília.
        Assert.Equal(new DateOnly(2026, 10, 10), SugestaoDeLista.DiaDaCompra(new DateTime(2026, 10, 11, 1, 0, 0, DateTimeKind.Utc)));
    }

    private static ItemDoModelo NoModelo(string nome) => ItemDoModelo.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nome, Guid.NewGuid(), UnidadeDeMedidaEnum.Unidade, 1, 0);

    [Fact]
    public void Item_da_lista_padrao_sem_compra_ha_mais_de_90_dias_aparece_como_parado()
    {
        var historico = new[] { Comprado("Sal", "2026-04", new DateOnly(2026, 4, 10)), Comprado("Leite", "2026-10", new DateOnly(2026, 10, 30)) };

        var parados = SugestaoDeLista.ModeloParado([NoModelo("sal"), NoModelo("Leite")], historico, new DateOnly(2026, 11, 6));

        var sal = Assert.Single(parados);
        Assert.Equal("sal", sal.Item.Nome);
        Assert.Equal(210, sal.DiasSemComprar);
    }

    [Fact]
    public void Item_de_ciclo_longo_ganha_folga_e_item_nunca_comprado_nao_e_marcado()
    {
        // Arroz comprado a cada ~4 meses: 100 dias sem comprar ainda está dentro do ritmo (limite = 2 ciclos).
        var historico = new[] { Comprado("Arroz", "2026-01", new DateOnly(2026, 1, 1)), Comprado("Arroz", "2026-05", new DateOnly(2026, 5, 1)), Comprado("Arroz", "2026-08", new DateOnly(2026, 8, 1)) };

        var parados = SugestaoDeLista.ModeloParado([NoModelo("Arroz"), NoModelo("Produto novo")], historico, new DateOnly(2026, 11, 6));

        Assert.Empty(parados);
    }
}
