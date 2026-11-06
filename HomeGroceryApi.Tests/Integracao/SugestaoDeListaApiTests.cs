namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class SugestaoDeListaApiTests
{
    private readonly ApiFactory _api;

    public SugestaoDeListaApiTests(ApiFactory api)
    {
        _api = api;
    }

    [PostgresFact]
    public async Task Sem_historico_a_sugestao_vem_vazia_e_a_lista_nasce_vazia()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, sugestao) = await u.GetAsync("/listas/sugestao?competencia=2026-12");
        var (criou, lista) = await u.PostAsync("/listas", new { competencia = "2026-12", origem = "Sugestao" });

        Assert.Equal(200, status);
        Assert.Equal(0, sugestao.GetProperty("itens").GetArrayLength());
        Assert.Equal(201, criou);
        Assert.Equal(0, lista.GetProperty("itens").GetArrayLength());
    }

    [PostgresFact]
    public async Task Chave_inventada_pelo_cliente_e_recusada()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, _) = await u.PostAsync("/listas", new { competencia = "2026-12", origem = "Sugestao", itensSugeridos = new[] { new { chave = "arroz|Kg", quantidade = 3 } } });

        Assert.Equal(400, status);
    }

    [PostgresFact]
    public async Task Competencia_invalida_na_sugestao_e_recusada_e_itens_sugeridos_so_valem_na_origem_sugestao()
    {
        var u = await _api.NovoUsuarioAsync();

        var (invalida, _) = await u.GetAsync("/listas/sugestao?competencia=dezembro");
        var (origemErrada, _) = await u.PostAsync("/listas", new { competencia = "2026-12", origem = "Zero", itensSugeridos = new[] { new { chave = "arroz|Kg" } } });

        Assert.True(invalida is 400 or 422);
        Assert.Equal(400, origemErrada);
    }

    [PostgresFact]
    public async Task Avisos_de_ritmo_de_quem_nao_tem_historico_vem_vazios()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, avisos) = await u.GetAsync("/analises/avisos");

        Assert.Equal(200, status);
        Assert.Equal(0, avisos.GetProperty("repor").GetArrayLength());
        Assert.Equal(0, avisos.GetProperty("listaPadraoParada").GetArrayLength());
    }

    [PostgresFact]
    public async Task Padroes_de_compra_de_quem_nao_tem_historico_vem_vazios()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, padroes) = await u.GetAsync("/analises/padroes");

        Assert.Equal(200, status);
        Assert.Equal(0, padroes.GetProperty("precosBons").GetArrayLength());
        Assert.Equal(0, padroes.GetProperty("sazonais").GetArrayLength());
        Assert.Equal(0, padroes.GetProperty("casados").GetArrayLength());
    }

    [PostgresFact]
    public async Task Habitos_de_compra_de_quem_nao_tem_historico_vem_vazios()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, habitos) = await u.GetAsync("/analises/habitos");

        Assert.Equal(200, status);
        Assert.Equal(0, habitos.GetProperty("mercados").GetArrayLength());
        Assert.Equal(0, habitos.GetProperty("responsaveis").GetArrayLength());
    }
}
