using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class PlanejadorDeRotaTests
{
    private static readonly Guid Grupo = Guid.NewGuid();
    private static readonly Guid Molhos = Guid.NewGuid();
    private static readonly Guid Copos = Guid.NewGuid();
    private static readonly Guid Mercearia = Guid.NewGuid();
    private static readonly Guid Limpeza = Guid.NewGuid();

    /// <summary>Mercado de 30 x 20 com entrada em (2, 18) e caixas em (24, 17).</summary>
    private static Loja NovaLoja() => Loja.Criar(Guid.NewGuid(), Grupo, "Teste", 30, 20, new PontoDoMapa(2, 18), new AreaDoMapa(24, 17, 5, 2));

    private static Secao NovaSecao(Loja loja, string nome, AreaDoMapa area, bool dividida = false, string? nomeB = null) => Secao.Criar(Guid.NewGuid(), loja.Id, Grupo, nome, nomeB, dividida, area);

    private static SecaoCategoria Vinculo(Loja loja, Secao secao, Guid categoria, LadoDaSecaoEnum lado = LadoDaSecaoEnum.A) => SecaoCategoria.Criar(Guid.NewGuid(), secao.Id, loja.Id, Grupo, categoria, lado);

    private static DemandaDeRota Demanda(Guid categoria, int ordem = 0) => new(Guid.NewGuid(), categoria, ordem);

    [Fact]
    public void Local_marcado_pelo_usuario_vale_mais_que_a_categoria()
    {
        var loja = NovaLoja();
        var daCategoria = NovaSecao(loja, "Bebidas", new AreaDoMapa(3, 12, 6, 2));
        var ondeAchou = NovaSecao(loja, "Ponta", new AreaDoMapa(18, 2, 6, 2));
        var demanda = new DemandaDeRota(Guid.NewGuid(), Mercearia, 0, [new LocalProprio(ondeAchou.Id, LadoDaSecaoEnum.A)]);

        var plano = PlanejadorDeRota.Planejar(loja, [daCategoria, ondeAchou], [Vinculo(loja, daCategoria, Mercearia)], [demanda], new PontoDoMapa(4, 15), true);

        Assert.Equal(ondeAchou.Id, Assert.Single(plano.Paradas).SecaoId);
    }

    [Fact]
    public void Item_com_dois_locais_proprios_vai_ao_mais_proximo_e_sem_categoria_ainda_entra_na_rota()
    {
        var loja = NovaLoja();
        var perto = NovaSecao(loja, "Perto", new AreaDoMapa(3, 12, 6, 2));
        var longe = NovaSecao(loja, "Longe", new AreaDoMapa(18, 2, 6, 2));
        var demanda = new DemandaDeRota(Guid.NewGuid(), null, 0, [new LocalProprio(perto.Id, LadoDaSecaoEnum.A), new LocalProprio(longe.Id, LadoDaSecaoEnum.A)]);

        var plano = PlanejadorDeRota.Planejar(loja, [perto, longe], [], [demanda], new PontoDoMapa(4, 15), true);

        Assert.Equal(perto.Id, Assert.Single(plano.Paradas).SecaoId);
        Assert.Empty(plano.ItensSemLocal);
    }

    [Fact]
    public void Local_proprio_de_secao_que_nao_existe_mais_cai_na_categoria()
    {
        var loja = NovaLoja();
        var daCategoria = NovaSecao(loja, "Bebidas", new AreaDoMapa(3, 12, 6, 2));
        var demanda = new DemandaDeRota(Guid.NewGuid(), Mercearia, 0, [new LocalProprio(Guid.NewGuid(), LadoDaSecaoEnum.A)]);

        var plano = PlanejadorDeRota.Planejar(loja, [daCategoria], [Vinculo(loja, daCategoria, Mercearia)], [demanda], new PontoDoMapa(4, 15), true);

        Assert.Equal(daCategoria.Id, Assert.Single(plano.Paradas).SecaoId);
    }

    [Fact]
    public void Gondola_de_dois_lados_para_no_corredor_do_lado_do_item()
    {
        var loja = NovaLoja();
        var gondola = NovaSecao(loja, "Molhos", new AreaDoMapa(8, 8, 10, 2), dividida: true, nomeB: "Copos descartáveis");
        var vinculos = new[] { Vinculo(loja, gondola, Molhos), Vinculo(loja, gondola, Copos, LadoDaSecaoEnum.B) };

        // Quem vem de cima estaria mais perto do lado A, mas o item é do lado B: a parada tem de ficar na metade de baixo
        // (o corredor de baixo ou a ponta dessa metade), nunca no corredor de cima.
        var plano = PlanejadorDeRota.Planejar(loja, [gondola], vinculos, [Demanda(Copos)], new PontoDoMapa(10, 3), true);

        var parada = Assert.Single(plano.Paradas);
        Assert.Equal(LadoDaSecaoEnum.B, parada.Lado);
        Assert.True(parada.Posicao.Y >= 9, $"parada em y={parada.Posicao.Y} está do lado de cima");
    }

    [Fact]
    public void Lado_A_da_mesma_gondola_para_no_corredor_de_cima()
    {
        var loja = NovaLoja();
        var gondola = NovaSecao(loja, "Molhos", new AreaDoMapa(8, 8, 10, 2), dividida: true, nomeB: "Copos descartáveis");
        var vinculos = new[] { Vinculo(loja, gondola, Molhos), Vinculo(loja, gondola, Copos, LadoDaSecaoEnum.B) };

        var plano = PlanejadorDeRota.Planejar(loja, [gondola], vinculos, [Demanda(Molhos)], new PontoDoMapa(10, 15), true);

        var parada = Assert.Single(plano.Paradas);
        Assert.Equal(LadoDaSecaoEnum.A, parada.Lado);
        Assert.True(parada.Posicao.Y <= 8, $"parada em y={parada.Posicao.Y} está do lado de baixo");
    }

    [Fact]
    public void Categoria_em_varios_lugares_vai_ao_mais_proximo()
    {
        var loja = NovaLoja();
        var perto = NovaSecao(loja, "Perto", new AreaDoMapa(3, 12, 6, 2));
        var longe = NovaSecao(loja, "Longe", new AreaDoMapa(18, 2, 6, 2));
        var vinculos = new[] { Vinculo(loja, perto, Mercearia), Vinculo(loja, longe, Mercearia) };

        var plano = PlanejadorDeRota.Planejar(loja, [perto, longe], vinculos, [Demanda(Mercearia)], loja.Entrada, true);

        Assert.Equal(perto.Id, Assert.Single(plano.Paradas).SecaoId);
    }

    [Fact]
    public void Categorias_no_mesmo_lugar_viram_uma_parada_so()
    {
        var loja = NovaLoja();
        var secao = NovaSecao(loja, "Corredor 1", new AreaDoMapa(8, 8, 10, 2));
        var vinculos = new[] { Vinculo(loja, secao, Mercearia), Vinculo(loja, secao, Limpeza) };
        var demandas = new[] { Demanda(Mercearia), Demanda(Mercearia), Demanda(Limpeza) };

        var plano = PlanejadorDeRota.Planejar(loja, [secao], vinculos, demandas, loja.Entrada, true);

        Assert.Equal(3, Assert.Single(plano.Paradas).ItensIds.Count);
    }

    [Fact]
    public void Item_de_categoria_sem_lugar_no_mapa_vai_para_sem_local()
    {
        var loja = NovaLoja();
        var secao = NovaSecao(loja, "Mercearia", new AreaDoMapa(8, 8, 10, 2));
        var semCategoria = new DemandaDeRota(Guid.NewGuid(), null, 0);
        var semLugar = Demanda(Limpeza);

        var plano = PlanejadorDeRota.Planejar(loja, [secao], [Vinculo(loja, secao, Mercearia)], [Demanda(Mercearia), semCategoria, semLugar], loja.Entrada, true);

        Assert.Single(plano.Paradas);
        Assert.Equivalent(new[] { semCategoria.ItemId, semLugar.ItemId }, plano.ItensSemLocal);
    }

    [Fact]
    public void Sem_nada_pendente_nao_ha_paradas_nem_distancia()
    {
        var loja = NovaLoja();

        var plano = PlanejadorDeRota.Planejar(loja, [], [], [], loja.Entrada, true);

        Assert.Empty(plano.Paradas);
        Assert.Equal(0, plano.Distancia);
    }

    [Fact]
    public void Rota_termina_colada_nos_caixas()
    {
        var loja = NovaLoja();
        var secao = NovaSecao(loja, "Mercearia", new AreaDoMapa(8, 8, 10, 2));

        var plano = PlanejadorDeRota.Planejar(loja, [secao], [Vinculo(loja, secao, Mercearia)], [Demanda(Mercearia)], loja.Entrada, true);

        var fim = plano.Trilha[^1];
        Assert.False(loja.Caixas.Contem(fim));
        Assert.True(Math.Abs(fim.X - Math.Clamp(fim.X, loja.Caixas.X, loja.Caixas.X + loja.Caixas.Largura - 1)) <= 1);
        Assert.True(Math.Abs(fim.Y - Math.Clamp(fim.Y, loja.Caixas.Y, loja.Caixas.Y + loja.Caixas.Altura - 1)) <= 1);
    }

    [Fact]
    public void Ponto_de_partida_em_cima_de_prateleira_volta_para_a_entrada()
    {
        var loja = NovaLoja();
        var secao = NovaSecao(loja, "Mercearia", new AreaDoMapa(8, 8, 10, 2));

        var plano = PlanejadorDeRota.Planejar(loja, [secao], [Vinculo(loja, secao, Mercearia)], [Demanda(Mercearia)], new PontoDoMapa(9, 8), true);

        Assert.Equal(loja.Entrada, plano.Trilha[0]);
    }

    [Fact]
    public void Rota_otimizada_nunca_e_mais_longa_que_seguir_a_ordem_das_categorias()
    {
        var loja = NovaLoja();
        var longe = NovaSecao(loja, "Longe", new AreaDoMapa(18, 2, 6, 2));
        var perto = NovaSecao(loja, "Perto", new AreaDoMapa(3, 12, 6, 2));
        var vinculos = new[] { Vinculo(loja, longe, Mercearia), Vinculo(loja, perto, Limpeza) };
        var demandas = new[] { Demanda(Mercearia, 0), Demanda(Limpeza, 1) };

        var otimizada = PlanejadorDeRota.Planejar(loja, [longe, perto], vinculos, demandas, loja.Entrada, true);
        var naOrdem = PlanejadorDeRota.Planejar(loja, [longe, perto], vinculos, demandas, loja.Entrada, false);

        Assert.True(otimizada.Distancia <= naOrdem.Distancia);
        Assert.Equal(perto.Id, otimizada.Paradas[0].SecaoId);
        Assert.Equal(longe.Id, naOrdem.Paradas[0].SecaoId);
    }

    [Fact]
    public void Texto_do_lado_acompanha_a_orientacao_da_gondola()
    {
        var loja = NovaLoja();
        var horizontal = NovaSecao(loja, "H", new AreaDoMapa(1, 1, 10, 2), dividida: true, nomeB: "B");
        var vertical = NovaSecao(loja, "V", new AreaDoMapa(15, 1, 2, 10), dividida: true, nomeB: "B");
        var simples = NovaSecao(loja, "S", new AreaDoMapa(20, 1, 4, 2));

        Assert.Equal("lado de baixo", horizontal.TextoDoLado(LadoDaSecaoEnum.B));
        Assert.Equal("lado esquerdo", vertical.TextoDoLado(LadoDaSecaoEnum.A));
        Assert.Null(simples.TextoDoLado(LadoDaSecaoEnum.A));
    }
}
