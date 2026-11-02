using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class ExperienciaAvancadaTests
{
    private readonly ApiFactory _api;

    public ExperienciaAvancadaTests(ApiFactory api)
    {
        _api = api;
    }

    private static async Task<Guid> NovaListaAsync(UsuarioDeTeste u, string competencia)
    {
        var (status, lista) = await u.PostAsync("/listas", new { competencia, origem = "Zero" });
        Assert.Equal(201, status);
        return lista.GetProperty("resumo").GetProperty("id").GetGuid();
    }

    /// <summary>Adiciona um item comprado com preço pago e devolve o id.</summary>
    private static async Task<Guid> ItemCompradoAsync(UsuarioDeTeste u, Guid lista, string nome, string categoria, decimal quantidade, decimal precoPago, decimal? precoEstimado = null)
    {
        var categoriaId = await u.CategoriaAsync(categoria);
        var (_, item) = await u.PostAsync($"/listas/{lista}/itens", new { nome, categoriaId, unidade = "Pacote", quantidade });
        var id = item.GetProperty("id").GetGuid();
        await u.PutAsync($"/listas/{lista}/itens/{id}", new { nome, categoriaId, unidade = "Pacote", quantidade, versao = item.GetProperty("versao").GetInt32(), precoUnitario = precoPago, precoEstimado });
        await u.PutAsync($"/listas/{lista}/itens/{id}/comprado", new { comprado = true });
        return id;
    }

    private static string[] Nomes(JsonElement itens) => itens.EnumerateArray().Select(i => i.GetProperty("nome").GetString()!).ToArray();

    [PostgresFact]
    public async Task Copiar_lista_anterior_cria_itens_novos_pendentes_e_sem_preco_pago()
    {
        var u = await _api.NovoUsuarioAsync();
        var outubro = await NovaListaAsync(u, "2026-10");
        await ItemCompradoAsync(u, outubro, "Arroz", "Mercearia", 2, 25m, precoEstimado: 24m);
        await ItemCompradoAsync(u, outubro, "Leite", "Laticínios", 6, 5.50m);

        var (status, nova) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = outubro });

        Assert.Equal(201, status);
        var itens = nova.GetProperty("itens");
        Assert.Equal(["Arroz", "Leite"], Nomes(itens));
        Assert.All(itens.EnumerateArray(), i =>
        {
            Assert.False(i.GetProperty("comprado").GetBoolean());
            Assert.Equal(JsonValueKind.Null, i.GetProperty("precoUnitario").ValueKind);
            Assert.Equal(JsonValueKind.Null, i.GetProperty("quantidadeComprada").ValueKind);
        });
        Assert.Equal(25m, itens[0].GetProperty("precoReferencia").GetDecimal());
        Assert.Equal(24m, itens[0].GetProperty("precoEstimado").GetDecimal());
        Assert.Equal(0m, nova.GetProperty("totais").GetProperty("realizado").GetDecimal());
        Assert.Equal(0, nova.GetProperty("resumo").GetProperty("percentual").GetInt32());
    }

    [PostgresFact]
    public async Task Preco_de_referencia_nunca_entra_no_total_gasto_do_novo_mes()
    {
        var u = await _api.NovoUsuarioAsync();
        var outubro = await NovaListaAsync(u, "2026-10");
        await ItemCompradoAsync(u, outubro, "Arroz", "Mercearia", 2, 25m);
        var (_, nova) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = outubro });
        var novaId = nova.GetProperty("resumo").GetProperty("id").GetGuid();
        var itemId = nova.GetProperty("itens")[0].GetProperty("id").GetGuid();

        await u.PutAsync($"/listas/{novaId}/itens/{itemId}/comprado", new { comprado = true });

        var (_, depois) = await u.GetAsync($"/listas/{novaId}");
        Assert.Equal(0m, depois.GetProperty("totais").GetProperty("realizado").GetDecimal());
        Assert.Equal(1, depois.GetProperty("totais").GetProperty("compradosSemPreco").GetInt32());
    }

    [PostgresFact]
    public async Task Lista_copiada_e_independente_da_original()
    {
        var u = await _api.NovoUsuarioAsync();
        var outubro = await NovaListaAsync(u, "2026-10");
        var arroz = await ItemCompradoAsync(u, outubro, "Arroz", "Mercearia", 2, 25m);
        var (_, nova) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = outubro });
        var novaId = nova.GetProperty("resumo").GetProperty("id").GetGuid();

        await u.DeleteAsync($"/listas/{novaId}/itens/{nova.GetProperty("itens")[0].GetProperty("id").GetGuid()}");

        var (_, original) = await u.GetAsync($"/listas/{outubro}");
        Assert.Equal(["Arroz"], Nomes(original.GetProperty("itens")));
        Assert.True(original.GetProperty("itens")[0].GetProperty("comprado").GetBoolean());
        Assert.Equal(arroz, original.GetProperty("itens")[0].GetProperty("id").GetGuid());
        Assert.Equal(50m, original.GetProperty("totais").GetProperty("realizado").GetDecimal());
    }

    [PostgresFact]
    public async Task Copiar_so_os_itens_selecionados_e_de_qualquer_mes()
    {
        var u = await _api.NovoUsuarioAsync();
        var agosto = await NovaListaAsync(u, "2026-08");
        await NovaListaAsync(u, "2026-10");
        await ItemCompradoAsync(u, agosto, "Arroz", "Mercearia", 2, 25m);
        var leite = await ItemCompradoAsync(u, agosto, "Leite", "Laticínios", 6, 5m);

        var (status, nova) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = agosto, itensDeOrigemIds = new[] { leite } });

        Assert.Equal(201, status);
        Assert.Equal(["Leite"], Nomes(nova.GetProperty("itens")));
    }

    [PostgresFact]
    public async Task Copiar_exige_a_lista_de_origem_e_nao_aceita_lista_de_outro_grupo()
    {
        var dona = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(dona, "2026-10");

        Assert.Equal(400, (await intruso.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar" })).Status);
        Assert.Equal(404, (await intruso.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = lista })).Status);
        Assert.Equal(404, (await intruso.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = Guid.NewGuid() })).Status);
        Assert.Equal(0, (await intruso.GetAsync("/listas")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Copiar_preserva_a_ordem_manual_e_respeita_a_regra_da_lista_principal()
    {
        var u = await _api.NovoUsuarioAsync();
        var outubro = await NovaListaAsync(u, "2026-10");
        var a = await ItemCompradoAsync(u, outubro, "Arroz", "Mercearia", 1, 1m);
        var b = await ItemCompradoAsync(u, outubro, "Banana", "Hortifruti", 1, 1m);
        await u.PutAsync($"/listas/{outubro}/itens/ordem", new { ids = new[] { b, a } });

        var (_, nova) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = outubro });

        Assert.Equal(["Banana", "Arroz"], Nomes(nova.GetProperty("itens")));
        Assert.Equal(422, (await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Copiar", listaDeOrigemId = outubro })).Status);
    }

    [PostgresFact]
    public async Task Reordenar_itens_da_lista_valida_o_conjunto_e_bloqueia_lista_finalizada()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        var a = await ItemCompradoAsync(u, lista, "Arroz", "Mercearia", 1, 1m);
        var b = await ItemCompradoAsync(u, lista, "Banana", "Hortifruti", 1, 1m);
        var c = await ItemCompradoAsync(u, lista, "Café", "Mercearia", 1, 1m);

        Assert.Equal(204, (await u.PutAsync($"/listas/{lista}/itens/ordem", new { ids = new[] { c, a, b } })).Status);
        var (_, atual) = await u.GetAsync($"/listas/{lista}");
        Assert.Equal(["Café", "Arroz", "Banana"], Nomes(atual.GetProperty("itens")));

        Assert.Equal(400, (await u.PutAsync($"/listas/{lista}/itens/ordem", new { ids = new[] { c, a } })).Status);
        Assert.Equal(400, (await u.PutAsync($"/listas/{lista}/itens/ordem", new { ids = new[] { c, a, a } })).Status);

        await u.PostAsync($"/listas/{lista}/finalizar");
        Assert.Equal(422, (await u.PutAsync($"/listas/{lista}/itens/ordem", new { ids = new[] { a, b, c } })).Status);
    }

    [PostgresFact]
    public async Task Favoritar_produto_e_sugerir_favoritos_e_recorrentes()
    {
        var u = await _api.NovoUsuarioAsync();
        var setembro = await NovaListaAsync(u, "2026-09");
        var outubro = await NovaListaAsync(u, "2026-10");
        await ItemCompradoAsync(u, setembro, "Café", "Mercearia", 1, 18m);
        await ItemCompradoAsync(u, outubro, "Café", "Mercearia", 1, 19m);
        await ItemCompradoAsync(u, outubro, "Sal", "Mercearia", 1, 3m);
        var (_, produtos) = await u.GetAsync("/produtos?q=chocolate");
        Assert.Equal(0, produtos.GetArrayLength());

        var (_, sal) = await u.GetAsync("/produtos?q=sal");
        var salId = sal[0].GetProperty("id").GetGuid();
        var (statusFavorito, favoritado) = await u.PutAsync($"/produtos/{salId}/favorito", new { favorito = true });

        Assert.Equal(200, statusFavorito);
        Assert.True(favoritado.GetProperty("favorito").GetBoolean());
        var (_, sugestoes) = await u.GetAsync("/produtos/sugestoes");
        Assert.Equal(["Sal", "Café"], Nomes(sugestoes));

        await u.PutAsync($"/produtos/{salId}/favorito", new { favorito = false });
        var (_, semFavorito) = await u.GetAsync("/produtos/sugestoes");
        Assert.Equal(["Café"], Nomes(semFavorito));
    }

    [PostgresFact]
    public async Task Favorito_e_sugestoes_so_valem_para_o_proprio_grupo()
    {
        var dona = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(dona, "2026-10");
        await ItemCompradoAsync(dona, lista, "Café", "Mercearia", 1, 18m);
        var (_, achados) = await dona.GetAsync("/produtos?q=caf");

        Assert.Equal(404, (await intruso.PutAsync($"/produtos/{achados[0].GetProperty("id").GetGuid()}/favorito", new { favorito = true })).Status);
        Assert.Equal(0, (await intruso.GetAsync("/produtos/sugestoes")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Excluir_categoria_move_todos_os_produtos_mesmo_com_mais_de_50_cadastrados()
    {
        var u = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(u, "2026-11");
        var bebidas = await u.CategoriaAsync("Bebidas");
        var outros = await u.CategoriaAsync("Outros");
        for (var i = 0; i < 55; i++) await u.PostAsync($"/listas/{lista}/itens", new { nome = $"Bebida {i:00}", categoriaId = bebidas, unidade = "Litro", quantidade = 1 });

        Assert.Equal(204, (await u.DeleteAsync($"/categorias/{bebidas}")).Status);

        var (_, produtos) = await u.GetAsync("/produtos");
        Assert.Equal(55, produtos.GetArrayLength());
        Assert.All(produtos.EnumerateArray(), p => Assert.Equal(outros, p.GetProperty("categoriaId").GetGuid()));
    }
}
