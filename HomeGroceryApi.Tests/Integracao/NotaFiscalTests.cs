using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class NotaFiscalTests
{
    private const string Cnpj = "93.209.765/0509-98";

    private readonly ApiFactory _api;

    public NotaFiscalTests(ApiFactory api)
    {
        _api = api;
    }

    private static async Task<(Guid Lista, Guid Arroz, Guid Queijo)> ListaComItensAsync(UsuarioDeTeste u)
    {
        var (_, lista) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero" });
        var id = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var (_, arroz) = await u.PostAsync($"/listas/{id}/itens", new { nome = "Biscoito wafer", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Unidade", quantidade = 1 });
        var (_, queijo) = await u.PostAsync($"/listas/{id}/itens", new { nome = "Queijo muçarela", categoriaId = await u.CategoriaAsync("Laticínios"), unidade = "Kg", quantidade = 0.5 });
        return (id, arroz.GetProperty("id").GetGuid(), queijo.GetProperty("id").GetGuid());
    }

    private static object Item(string codigo, string nome, decimal qtd, decimal preco, Guid? itemId = null, Guid? categoriaId = null, string? unidade = null)
        => new { codigo, nome, quantidade = qtd, precoUnitario = preco, itemId, categoriaId, unidade };

    [PostgresFact]
    public async Task Importar_nota_preenche_preco_e_quantidade_marca_comprado_e_traz_o_que_nao_estava_na_lista()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, queijo) = await ListaComItensAsync(u);
        var pizza = await u.CategoriaAsync("Congelados");

        var (status, resposta) = await u.PostAsync($"/listas/{lista}/nota", new
        {
            cnpj = Cnpj,
            itens = new[]
            {
                Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer),
                Item("AR032886", "QJO.M.PADR.TIROLEZ", 0.48m, 69.9m, itemId: queijo),
                Item("AR002452", "M.PIZZA M.LEVE FRIG.", 1, 9.5m, categoriaId: pizza, unidade: "Unidade"),
            },
        });

        Assert.Equal(200, status);
        var itens = resposta.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(3, itens.Count);
        var queijoItem = itens.Single(i => i.GetProperty("id").GetGuid() == queijo);
        Assert.True(queijoItem.GetProperty("comprado").GetBoolean());
        Assert.Equal(0.48m, queijoItem.GetProperty("quantidadeComprada").GetDecimal());
        Assert.Equal(69.9m, queijoItem.GetProperty("precoUnitario").GetDecimal());
        Assert.Contains(itens, i => i.GetProperty("nome").GetString() == "M.PIZZA M.LEVE FRIG." && i.GetProperty("comprado").GetBoolean());

        // 4,39 + 0,48 x 69,90 (33,55) + 9,50 = 47,44: o gasto do mês bate com a nota.
        Assert.Equal(47.44m, resposta.GetProperty("totais").GetProperty("realizado").GetDecimal());
        Assert.Equal(3, resposta.GetProperty("resumo").GetProperty("comprados").GetInt32());
    }

    [PostgresFact]
    public async Task Os_pares_codigo_produto_ficam_guardados_para_as_proximas_notas_do_mesmo_mercado()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, _) = await ListaComItensAsync(u);
        var (_, itens) = await u.GetAsync($"/listas/{lista}");
        var produtoDoWafer = itens.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == wafer).GetProperty("produtoId").GetGuid();

        Assert.Equal(0, (await u.GetAsync("/notas/vinculos?cnpj=93209765050998")).Corpo.GetArrayLength());
        await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer) } });

        // O CNPJ vale com ou sem pontuação.
        foreach (var cnpj in new[] { "93209765050998", Uri.EscapeDataString(Cnpj) })
        {
            var (_, vinculos) = await u.GetAsync($"/notas/vinculos?cnpj={cnpj}");
            var par = Assert.Single(vinculos.EnumerateArray());
            Assert.Equal("AR039393", par.GetProperty("codigo").GetString());
            Assert.Equal(produtoDoWafer, par.GetProperty("produtoId").GetGuid());
        }

        // Outro mercado (outro CNPJ) não herda os códigos.
        Assert.Equal(0, (await u.GetAsync("/notas/vinculos?cnpj=11222333000181")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Importar_a_mesma_nota_de_novo_da_conflito_e_nao_conta_duas_vezes()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, _) = await ListaComItensAsync(u);
        var nota = new { cnpj = Cnpj, itens = new[] { Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer) } };

        Assert.Equal(200, (await u.PostAsync($"/listas/{lista}/nota", nota)).Status);
        var (status, erro) = await u.PostAsync($"/listas/{lista}/nota", nota);

        Assert.Equal(409, status);
        Assert.Contains("já foi importada", erro.GetProperty("detail").GetString());
        var (_, atual) = await u.GetAsync($"/listas/{lista}");
        Assert.Equal(4.39m, atual.GetProperty("totais").GetProperty("realizado").GetDecimal());
    }

    [PostgresFact]
    public async Task Duas_linhas_da_nota_para_o_mesmo_item_viram_uma_compra_com_preco_medio()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, _, queijo) = await ListaComItensAsync(u);

        var (status, resposta) = await u.PostAsync($"/listas/{lista}/nota", new
        {
            cnpj = Cnpj,
            itens = new[] { Item("AR032886", "QJO.M.PADR.TIROLEZ", 0.4m, 70m, itemId: queijo), Item("AR032887", "QJO.M.PADR.TIROLEZ", 0.6m, 50m, itemId: queijo) },
        });

        Assert.Equal(200, status);
        var item = resposta.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == queijo);
        Assert.Equal(1m, item.GetProperty("quantidadeComprada").GetDecimal());
        Assert.Equal(58m, item.GetProperty("precoUnitario").GetDecimal());
    }

    [PostgresFact]
    public async Task Importar_valida_a_nota_a_lista_e_o_grupo()
    {
        var u = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        var (lista, wafer, _) = await ListaComItensAsync(u);
        var itemValido = Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer);

        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = Array.Empty<object>() })).Status);
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = "123", itens = new[] { itemValido } })).Status);
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("X1", "Novo sem categoria", 1, 2m) } })).Status);
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("AR039393", "x", 0, 4.39m, itemId: wafer) } })).Status);
        Assert.Equal(404, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("AR1", "x", 1, 1m, itemId: Guid.NewGuid()) } })).Status);
        Assert.Equal(404, (await intruso.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { itemValido } })).Status);

    }

    [PostgresFact]
    public async Task Importar_depois_de_finalizar_a_compra_atualiza_o_total_congelado_nota_a_nota()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, queijo) = await ListaComItensAsync(u);
        await u.PostAsync($"/listas/{lista}/finalizar");

        // Primeira nota (mercado A) e segunda nota (mercado B) da mesma compra, importadas em casa.
        var (statusA, _) = await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer) } });
        var (statusB, _) = await u.PostAsync($"/listas/{lista}/nota", new { cnpj = "11.222.333/0001-81", itens = new[] { Item("Q1", "QUEIJO", 0.5m, 40m, itemId: queijo) } });

        Assert.Equal(200, statusA);
        Assert.Equal(200, statusB);
        var (_, atual) = await u.GetAsync($"/listas/{lista}");
        Assert.Equal("Finalizada", atual.GetProperty("resumo").GetProperty("status").GetString());
        Assert.Equal(24.39m, atual.GetProperty("resumo").GetProperty("totalRealizado").GetDecimal());
        Assert.Equal(24.39m, atual.GetProperty("totais").GetProperty("realizado").GetDecimal());
    }

    [PostgresFact]
    public async Task O_mercado_escolhido_aprende_o_cnpj_e_a_compra_fica_registrada_nele()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, _) = await ListaComItensAsync(u);
        var (_, loja) = await u.PostAsync("/lojas", new { nome = "WMS Araras", cidade = "Araras" });
        var lojaId = loja.GetProperty("id").GetGuid();

        var (status, resposta) = await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, lojaId, itens = new[] { Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer) } });

        Assert.Equal(200, status);
        var item = resposta.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == wafer);
        Assert.Equal(lojaId, item.GetProperty("compradoNaLojaId").GetGuid());
        var (_, lojas) = await u.GetAsync("/lojas");
        Assert.Equal("93209765050998", lojas.EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == lojaId).GetProperty("cnpj").GetString());
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, lojaId = Guid.NewGuid(), itens = new[] { Item("X", "x", 1, 1m, itemId: wafer) } })).Status);
    }

    // Chave de acesso real de uma NFC-e de SP: UF 35, AAMM 2610, CNPJ 93209765050998 (dígitos 7 a 20).
    private const string Chave = "35261093209765050998655340000029561048579171";

    [PostgresFact]
    public async Task Chave_de_acesso_reconhece_a_nota_ja_importada_antes_mesmo_de_colar_o_texto()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, queijo) = await ListaComItensAsync(u);

        Assert.False((await u.GetAsync($"/notas/importada?chave={Chave}")).Corpo.GetProperty("importada").GetBoolean());
        Assert.Equal(200, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, chaveDeAcesso = Chave, itens = new[] { Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer) } })).Status);
        Assert.True((await u.GetAsync($"/notas/importada?chave={Chave}")).Corpo.GetProperty("importada").GetBoolean());

        // Mesma chave com itens diferentes (texto colado de novo, outro recorte): continua sendo a mesma nota.
        var (status, erro) = await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, chaveDeAcesso = Chave, itens = new[] { Item("AR032886", "QJO", 0.5m, 40m, itemId: queijo) } });
        Assert.Equal(409, status);
        Assert.Contains("já foi importada", erro.GetProperty("detail").GetString());

        // Outro grupo não vê nem é barrado por ela.
        var outro = await _api.NovoUsuarioAsync();
        Assert.False((await outro.GetAsync($"/notas/importada?chave={Chave}")).Corpo.GetProperty("importada").GetBoolean());
    }

    [PostgresFact]
    public async Task Chave_valida_o_formato_e_o_cnpj_e_serve_de_cnpj_quando_ele_nao_vem()
    {
        var u = await _api.NovoUsuarioAsync();
        var (lista, wafer, _) = await ListaComItensAsync(u);
        var item = Item("AR039393", "BISC.BAUDUCCO WAFER", 1, 4.39m, itemId: wafer);

        Assert.Equal(400, (await u.GetAsync("/notas/importada?chave=123")).Status);
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, chaveDeAcesso = "123", itens = new[] { item } })).Status);
        // O CNPJ do texto não é o do emitente da chave: QR de uma nota, texto de outra.
        Assert.Equal(400, (await u.PostAsync($"/listas/{lista}/nota", new { cnpj = "11.222.333/0001-81", chaveDeAcesso = Chave, itens = new[] { item } })).Status);

        // Sem CNPJ no pedido, o da chave é usado: o par código/produto fica guardado para o mercado certo.
        Assert.Equal(200, (await u.PostAsync($"/listas/{lista}/nota", new { chaveDeAcesso = Chave, itens = new[] { item } })).Status);
        Assert.Equal(1, (await u.GetAsync("/notas/vinculos?cnpj=93209765050998")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Item_que_ja_entra_comprado_pela_nota_nao_avisa_a_familia_como_pedido()
    {
        var eu = await _api.NovoUsuarioAsync();
        var esposa = await _api.NovoUsuarioAsync();
        var (_, convite) = await eu.PostAsync("/grupo/convites");
        var (_, sessao) = await esposa.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        esposa.AplicarSessao(sessao);
        var (lista, _, _) = await ListaComItensAsync(eu);

        var (status, _) = await esposa.PostAsync($"/listas/{lista}/nota", new { cnpj = Cnpj, itens = new[] { Item("AR1", "Café da nota", 1, 12m, categoriaId: await esposa.CategoriaAsync("Mercearia")) } });

        Assert.Equal(200, status);
        Assert.DoesNotContain(_api.Avisos.Enviados, e => e.Notificacao.Corpo.Contains("Café da nota"));
    }
}
