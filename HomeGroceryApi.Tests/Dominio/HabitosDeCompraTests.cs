using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class HabitosDeCompraTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Bruno = Guid.NewGuid();
    private static readonly Guid Carnes = Guid.NewGuid();
    private static readonly Guid Padaria = Guid.NewGuid();
    private static readonly Guid Savegnago = Guid.NewGuid();
    private static readonly Guid Atacadao = Guid.NewGuid();

    private static ItemComCompetencia Comprado(string nome, Guid? categoria, Guid? loja, Guid quem, string categoriaNome = "Açougue")
    {
        var item = ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nome, categoria, categoriaNome, UnidadeDeMedidaEnum.Unidade, 1, 0, quem);
        item.DefinirComprado(true, quem, new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc), loja);
        return new ItemComCompetencia("2026-10", item);
    }

    [Fact]
    public void Produto_comprado_quase_sempre_no_mesmo_mercado_tem_mercado_habitual()
    {
        var itens = new[]
        {
            Comprado("Arroz", Carnes, Atacadao, Ana), Comprado("arroz", Carnes, Atacadao, Ana), Comprado("Arroz", Carnes, Savegnago, Ana),
        };

        var habitual = Assert.Single(HabitosDeCompra.MercadoPorProduto(itens));

        Assert.Equal(Atacadao, habitual.LojaId);
        Assert.Equal(2, habitual.Compras);
        Assert.Equal(3, habitual.Total);
    }

    [Fact]
    public void Sem_repeticao_ou_sem_maioria_clara_nao_ha_mercado_habitual()
    {
        var itens = new[]
        {
            Comprado("Leite", Carnes, Atacadao, Ana),
            Comprado("Café", Carnes, Atacadao, Ana), Comprado("Café", Carnes, Savegnago, Ana),
            Comprado("Sal", Carnes, null, Ana), Comprado("Sal", Carnes, null, Ana),
        };

        Assert.Empty(HabitosDeCompra.MercadoPorProduto(itens));
    }

    [Fact]
    public void Categoria_que_uma_pessoa_quase_sempre_compra_tem_responsavel_habitual()
    {
        var itens = new[]
        {
            Comprado("Carne", Carnes, null, Ana), Comprado("Frango", Carnes, null, Ana), Comprado("Linguiça", Carnes, null, Bruno),
            Comprado("Pão", Padaria, null, Bruno, "Padaria"), Comprado("Bolo", Padaria, null, Bruno, "Padaria"), Comprado("Rosca", Padaria, null, Bruno, "Padaria"),
        };

        var responsaveis = HabitosDeCompra.ResponsavelPorCategoria(itens, new HashSet<Guid> { Ana, Bruno });

        Assert.Equal(2, responsaveis.Count);
        Assert.Equal(Ana, responsaveis.Single(r => r.CategoriaId == Carnes).UsuarioId);
        Assert.Equal(Bruno, responsaveis.Single(r => r.CategoriaId == Padaria).UsuarioId);
    }

    [Fact]
    public void Quem_saiu_do_grupo_nao_conta_e_uma_pessoa_so_nao_gera_sugestao()
    {
        var sozinha = new[] { Comprado("Carne", Carnes, null, Ana), Comprado("Frango", Carnes, null, Ana), Comprado("Linguiça", Carnes, null, Ana) };
        Assert.Empty(HabitosDeCompra.ResponsavelPorCategoria(sozinha, new HashSet<Guid> { Ana, Bruno }));

        var comQuemSaiu = new[] { Comprado("Carne", Carnes, null, Ana), Comprado("Frango", Carnes, null, Ana), Comprado("Linguiça", Carnes, null, Bruno) };
        Assert.Empty(HabitosDeCompra.ResponsavelPorCategoria(comQuemSaiu, new HashSet<Guid> { Bruno }));
    }

    [Fact]
    public void Categoria_dividida_meio_a_meio_nao_tem_responsavel_habitual()
    {
        var itens = new[] { Comprado("Carne", Carnes, null, Ana), Comprado("Frango", Carnes, null, Bruno), Comprado("Linguiça", Carnes, null, Ana), Comprado("Bife", Carnes, null, Bruno) };

        Assert.Empty(HabitosDeCompra.ResponsavelPorCategoria(itens, new HashSet<Guid> { Ana, Bruno }));
    }
}
