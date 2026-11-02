using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class FinanceiroApiTests
{
    private readonly ApiFactory _api;

    public FinanceiroApiTests(ApiFactory api)
    {
        _api = api;
    }

    /// <summary>Cria uma lista do zero e devolve o id dela.</summary>
    private static async Task<Guid> NovaListaAsync(UsuarioDeTeste u, string competencia)
    {
        var (status, lista) = await u.PostAsync("/listas", new { competencia, origem = "Zero" });
        Assert.Equal(201, status);
        return lista.GetProperty("resumo").GetProperty("id").GetGuid();
    }

    /// <summary>Adiciona um item e, opcionalmente, define preço e marca como comprado. Devolve o item final.</summary>
    private static async Task<JsonElement> ItemAsync(UsuarioDeTeste u, Guid listaId, string nome, string categoria, string unidade, decimal quantidade, decimal? precoPago = null, decimal? precoEstimado = null, bool comprado = true, decimal? quantidadeComprada = null)
    {
        var categoriaId = await u.CategoriaAsync(categoria);
        var (status, item) = await u.PostAsync($"/listas/{listaId}/itens", new { nome, categoriaId, unidade, quantidade });
        Assert.Equal(201, status);
        var itemId = item.GetProperty("id").GetGuid();

        if (precoPago is not null || precoEstimado is not null || quantidadeComprada is not null)
        {
            (status, item) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome, categoriaId, unidade, quantidade, versao = item.GetProperty("versao").GetInt32(), quantidadeComprada, precoEstimado, precoUnitario = precoPago });
            Assert.Equal(200, status);
        }

        if (comprado) (status, item) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });
        return item;
    }

    [PostgresFact]
    public async Task Totais_da_lista_seguem_o_exemplo_da_especificacao()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        await ItemAsync(u, lista, "Arroz", "Mercearia", "Pacote", 2, precoPago: 25m, precoEstimado: 25m);
        await ItemAsync(u, lista, "Leite", "Laticínios", "Litro", 6, precoPago: 5.50m, precoEstimado: 5.50m);
        await ItemAsync(u, lista, "Café", "Mercearia", "Pacote", 1, precoPago: 18m, precoEstimado: 18m);

        var (_, resposta) = await u.GetAsync($"/listas/{lista}");

        var totais = resposta.GetProperty("totais");
        Assert.Equal(101.00m, totais.GetProperty("realizado").GetDecimal());
        Assert.Equal(101.00m, totais.GetProperty("estimado").GetDecimal());
        Assert.Equal(0m, totais.GetProperty("restante").GetDecimal());
        var porCategoria = totais.GetProperty("porCategoria");
        Assert.Equal("Mercearia", porCategoria[0].GetProperty("categoria").GetString());
        Assert.Equal(68m, porCategoria[0].GetProperty("realizado").GetDecimal());
        Assert.Equal(101.00m, resposta.GetProperty("resumo").GetProperty("totalRealizado").GetDecimal());
    }

    [PostgresFact]
    public async Task Preco_estimado_nao_vira_gasto_e_quantidade_comprada_vale_no_total()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        await ItemAsync(u, lista, "Queijo", "Laticínios", "Kg", 1, precoEstimado: 50m);
        var carne = await ItemAsync(u, lista, "Carne", "Açougue", "Kg", 1.5m, precoPago: 40m, precoEstimado: 38m, quantidadeComprada: 1.2m);

        var (_, resposta) = await u.GetAsync($"/listas/{lista}");

        var totais = resposta.GetProperty("totais");
        Assert.Equal(48.00m, totais.GetProperty("realizado").GetDecimal());
        Assert.Equal(107.00m, totais.GetProperty("estimado").GetDecimal());
        Assert.Equal(1, totais.GetProperty("compradosSemPreco").GetInt32());
        Assert.Equal(48.00m, carne.GetProperty("totalRealizado").GetDecimal());
    }

    [PostgresFact]
    public async Task Preco_de_um_mes_nao_altera_o_preco_de_outro_mes()
    {
        var u = await _api.NovoUsuarioAsync();
        var outubro = await NovaListaAsync(u, "2026-10");
        var novembro = await NovaListaAsync(u, "2026-11");
        await ItemAsync(u, outubro, "Café", "Mercearia", "Pacote", 1, precoPago: 18m);
        await ItemAsync(u, novembro, "Café", "Mercearia", "Pacote", 1, precoPago: 24m);

        var (_, lOut) = await u.GetAsync($"/listas/{outubro}");
        var (_, lNov) = await u.GetAsync($"/listas/{novembro}");

        Assert.Equal(18m, lOut.GetProperty("itens")[0].GetProperty("precoUnitario").GetDecimal());
        Assert.Equal(24m, lNov.GetProperty("itens")[0].GetProperty("precoUnitario").GetDecimal());
    }

    [PostgresFact]
    public async Task Finalizar_congela_o_total_gasto_e_reabrir_volta_a_calcular()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        var arroz = await ItemAsync(u, lista, "Arroz", "Mercearia", "Pacote", 2, precoPago: 25m);

        var (_, finalizada) = await u.PostAsync($"/listas/{lista}/finalizar");
        Assert.Equal(50m, finalizada.GetProperty("totalRealizado").GetDecimal());

        var (statusEditar, _) = await u.PutAsync($"/listas/{lista}/itens/{arroz.GetProperty("id").GetGuid()}", new { nome = "Arroz", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 2, versao = arroz.GetProperty("versao").GetInt32(), precoUnitario = 99m });
        Assert.Equal(422, statusEditar);
        var (_, historico) = await u.GetAsync("/listas");
        Assert.Equal(50m, historico[0].GetProperty("totalRealizado").GetDecimal());

        await u.PostAsync($"/listas/{lista}/reabrir");
        var (_, reaberta) = await u.GetAsync($"/listas/{lista}");
        Assert.Equal("EmAndamento", reaberta.GetProperty("resumo").GetProperty("status").GetString());
        Assert.Equal(50m, reaberta.GetProperty("resumo").GetProperty("totalRealizado").GetDecimal());
    }

    [PostgresFact]
    public async Task Valida_precos_negativos_e_com_mais_de_duas_casas()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        var categoria = await u.CategoriaAsync("Mercearia");
        var (_, item) = await u.PostAsync($"/listas/{lista}/itens", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1 });
        var caminho = $"/listas/{lista}/itens/{item.GetProperty("id").GetGuid()}";
        var versao = item.GetProperty("versao").GetInt32();

        Assert.Equal(400, (await u.PutAsync(caminho, new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1, versao, precoUnitario = -1 })).Status);
        Assert.Equal(400, (await u.PutAsync(caminho, new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1, versao, precoUnitario = 1.999 })).Status);
        Assert.Equal(400, (await u.PutAsync(caminho, new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1, versao, quantidadeComprada = 0 })).Status);
        Assert.Equal(200, (await u.PutAsync(caminho, new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1, versao, precoUnitario = 0 })).Status);
    }

    [PostgresFact]
    public async Task Analise_de_gastos_traz_variacao_mensal_categorias_e_frequentes()
    {
        var u = await _api.NovoUsuarioAsync();
        var set = await NovaListaAsync(u, "2026-09");
        var out_ = await NovaListaAsync(u, "2026-10");
        await ItemAsync(u, set, "Arroz", "Mercearia", "Pacote", 1, precoPago: 100m);
        await ItemAsync(u, out_, "Arroz", "Mercearia", "Pacote", 1, precoPago: 60m);
        await ItemAsync(u, out_, "Leite", "Laticínios", "Litro", 1, precoPago: 60m);

        var (status, analise) = await u.GetAsync("/analises/gastos?meses=6");

        Assert.Equal(200, status);
        var meses = analise.GetProperty("meses");
        Assert.Equal(2, meses.GetArrayLength());
        Assert.Equal("2026-09", meses[0].GetProperty("competencia").GetString());
        Assert.Equal(JsonValueKind.Null, meses[0].GetProperty("variacaoPercentual").ValueKind);
        Assert.Equal(20.0m, meses[1].GetProperty("variacaoPercentual").GetDecimal());
        Assert.Equal("2026-10", analise.GetProperty("competenciaDasCategorias").GetString());
        Assert.Equal(["Laticínios", "Mercearia"], analise.GetProperty("categorias").EnumerateArray().Select(c => c.GetProperty("categoria").GetString()!).OrderBy(x => x).ToArray());
        Assert.Equal("Arroz", analise.GetProperty("frequentes")[0].GetProperty("nome").GetString());
        Assert.Equal(2, analise.GetProperty("frequentes")[0].GetProperty("compras").GetInt32());
    }

    [PostgresFact]
    public async Task Analise_de_precos_mostra_historico_e_maiores_aumentos()
    {
        var u = await _api.NovoUsuarioAsync();
        var set = await NovaListaAsync(u, "2026-09");
        var out_ = await NovaListaAsync(u, "2026-10");
        var nov = await NovaListaAsync(u, "2026-11");
        await ItemAsync(u, set, "Café", "Mercearia", "Pacote", 1, precoPago: 16m);
        await ItemAsync(u, out_, "Café", "Mercearia", "Pacote", 1, precoPago: 18m);
        await ItemAsync(u, nov, "Café", "Mercearia", "Pacote", 1, precoPago: 20m);
        await ItemAsync(u, out_, "Leite", "Laticínios", "Litro", 1, precoPago: 5m);
        await ItemAsync(u, nov, "Leite", "Laticínios", "Litro", 1, precoPago: 4m);

        var (_, analise) = await u.GetAsync("/analises/precos");

        var cafe = analise.GetProperty("produtos").EnumerateArray().First(p => p.GetProperty("nome").GetString() == "Café");
        Assert.Equal(20m, cafe.GetProperty("ultimoPreco").GetDecimal());
        Assert.Equal(16m, cafe.GetProperty("menorPreco").GetDecimal());
        Assert.Equal(18m, cafe.GetProperty("precoMedio").GetDecimal());
        Assert.Equal(3, cafe.GetProperty("pontos").GetArrayLength());
        Assert.Equal(11.1m, cafe.GetProperty("variacaoPercentual").GetDecimal());
        var aumentos = analise.GetProperty("maioresAumentos");
        Assert.Equal(1, aumentos.GetArrayLength());
        Assert.Equal("Café", aumentos[0].GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Analises_so_enxergam_os_dados_do_proprio_grupo()
    {
        var dona = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(dona, "2026-11");
        await ItemAsync(dona, lista, "Arroz", "Mercearia", "Pacote", 1, precoPago: 25m);

        var (_, gastos) = await intruso.GetAsync("/analises/gastos");
        var (_, precos) = await intruso.GetAsync("/analises/precos");

        Assert.Equal(0, gastos.GetProperty("meses").GetArrayLength());
        Assert.Equal(0, precos.GetProperty("produtos").GetArrayLength());
        Assert.Equal(401, (int)(await _api.CreateClient().GetAsync("/analises/gastos")).StatusCode);
    }

    [PostgresFact]
    public async Task Analises_sem_nenhuma_compra_respondem_vazio_sem_erro()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, gastos) = await u.GetAsync("/analises/gastos");

        Assert.Equal(200, status);
        Assert.Equal(0, gastos.GetProperty("meses").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, gastos.GetProperty("competenciaDasCategorias").ValueKind);
    }
}
