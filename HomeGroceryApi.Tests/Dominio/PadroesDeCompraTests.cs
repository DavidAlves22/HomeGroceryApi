using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class PadroesDeCompraTests
{
    private static readonly Guid Usuario = Guid.NewGuid();

    private static ItemComCompetencia Comprado(string nome, DateOnly dia, string? competencia = null)
    {
        var item = ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nome, Guid.NewGuid(), "Mercearia", UnidadeDeMedidaEnum.Unidade, 1, 0, Usuario);
        item.DefinirComprado(true, Usuario, dia.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc));
        return new ItemComCompetencia(competencia ?? $"{dia.Year}-{dia.Month:00}", item);
    }

    private static SerieDePreco Serie(string nome, int meses, decimal ultimo, decimal medio)
    {
        var pontos = Enumerable.Range(1, meses).Select(m => new PontoDePreco($"2026-{m:00}", medio)).ToList();
        return new SerieDePreco(nome, UnidadeDeMedidaEnum.Unidade, pontos, ultimo, Math.Min(ultimo, medio), Math.Max(ultimo, medio), medio, null);
    }

    [Fact]
    public void Preco_bom_exige_historico_e_queda_de_pelo_menos_10_por_cento()
    {
        var series = new[]
        {
            Serie("Azeite", 4, 22m, 28.75m),
            Serie("Café", 4, 18m, 19m),
            Serie("Sal", 2, 1m, 3m),
        };

        var bons = PadroesDeCompra.PrecosBons(series);

        var azeite = Assert.Single(bons);
        Assert.Equal("Azeite", azeite.Nome);
        Assert.Equal(23, azeite.PercentualAbaixo);
    }

    [Fact]
    public void Panetone_de_dezembro_do_ano_passado_aparece_na_lista_de_dezembro()
    {
        var itens = new[] { Comprado("Panetone", new DateOnly(2025, 12, 20)) };

        var sazonais = PadroesDeCompra.Sazonais(itens, Competencia.Parse("2026-12"), new DateOnly(2026, 11, 28));

        var panetone = Assert.Single(sazonais);
        Assert.Equal([2025], panetone.Anos);
        Assert.Equal(new DateOnly(2025, 12, 20), panetone.UltimaCompraNoMes);
    }

    [Fact]
    public void Sazonal_nao_inclui_o_que_ja_tem_ritmo_foi_comprado_ha_pouco_ou_ja_esta_pendente()
    {
        var hoje = new DateOnly(2026, 12, 2);
        var itens = new[]
        {
            // Ritmo mensal: a sugestão da lista já cuida dele.
            Comprado("Leite", new DateOnly(2025, 12, 5)), Comprado("Leite", new DateOnly(2026, 1, 5)), Comprado("Leite", new DateOnly(2026, 11, 28)),
            // Comprado em dezembro passado e de novo há 20 dias: já está em uso.
            Comprado("Chocolate", new DateOnly(2025, 12, 10)), Comprado("Chocolate", new DateOnly(2026, 11, 12)),
            // Já pendente na lista do mês.
            Comprado("Nozes", new DateOnly(2025, 12, 10)),
            new ItemComCompetencia("2026-12", ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "nozes", null, "Mercearia", UnidadeDeMedidaEnum.Unidade, 1, 0, Usuario)),
            // De fato sazonal.
            Comprado("Peru", new DateOnly(2025, 12, 22)),
        };

        var sazonais = PadroesDeCompra.Sazonais(itens, Competencia.Parse("2026-12"), hoje);

        Assert.Equal(["Peru"], sazonais.Select(s => s.Referencia.NomeSnapshot));
    }

    [Fact]
    public void Compras_do_mesmo_mes_no_mesmo_ano_nao_contam_como_sazonais()
    {
        var itens = new[] { Comprado("Panetone", new DateOnly(2026, 12, 1)) };

        Assert.Empty(PadroesDeCompra.Sazonais(itens, Competencia.Parse("2026-12"), new DateOnly(2026, 12, 20)));
    }

    [Fact]
    public void Cafe_e_acucar_que_vao_juntos_viram_par_e_o_que_vai_em_toda_ida_nao()
    {
        var itens = new List<ItemComCompetencia>();
        var dia = new DateOnly(2026, 1, 1);
        // 6 idas: arroz em todas (lift 1 com qualquer um); café + açúcar nas 3 primeiras; sabão nas 3 últimas.
        for (var ida = 0; ida < 6; ida++)
        {
            var data = dia.AddDays(ida * 7);
            itens.Add(Comprado("Arroz", data));
            if (ida < 3)
            {
                itens.Add(Comprado("Café", data));
                itens.Add(Comprado("Açúcar", data));
            }
            else
            {
                itens.Add(Comprado("Sabão", data));
            }
        }

        var pares = PadroesDeCompra.Casados(itens);

        var par = Assert.Single(pares);
        Assert.Equal(["Açúcar", "Café"], new[] { par.Primeiro, par.Segundo }.Order().ToArray());
        Assert.Equal(3, par.Juntos);
        Assert.Equal(6, par.Idas);
        Assert.Equal(2m, par.Lift);
    }

    [Fact]
    public void Poucas_idas_ao_mercado_nao_formam_pares()
    {
        var itens = new List<ItemComCompetencia>();
        for (var ida = 0; ida < 4; ida++)
        {
            var data = new DateOnly(2026, 1, 1).AddDays(ida * 7);
            itens.Add(Comprado("Café", data));
            itens.Add(Comprado("Açúcar", data));
        }

        Assert.Empty(PadroesDeCompra.Casados(itens));
    }
}
