using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class FluxosPrincipaisTests
{
    private readonly ApiFactory _api;

    public FluxosPrincipaisTests(ApiFactory api)
    {
        _api = api;
    }

    private static async Task<Guid> AdicionarAoModeloAsync(UsuarioDeTeste u, string nome, string categoria, string unidade = "Pacote", decimal quantidade = 2)
    {
        var (status, item) = await u.PostAsync("/modelo/itens", new { nome, categoriaId = await u.CategoriaAsync(categoria), unidade, quantidade });
        Assert.Equal(201, status);
        return item.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CriarListaAsync(UsuarioDeTeste u, string competencia, string origem = "Padrao", Guid[]? itensDoModelo = null, bool permitirDuplicada = false, string? nome = null)
    {
        var (status, lista) = await u.PostAsync("/listas", new { competencia, nome, origem, itensDoModeloIds = itensDoModelo, permitirDuplicada });
        Assert.Equal(201, status);
        return lista;
    }

    private static string[] Nomes(JsonElement itens) => itens.EnumerateArray().Select(i => i.GetProperty("nome").GetString()!).ToArray();

    [PostgresFact]
    public async Task Cadastro_cria_as_11_categorias_iniciais_na_ordem()
    {
        var u = await _api.NovoUsuarioAsync();

        var (status, categorias) = await u.GetAsync("/categorias");

        Assert.Equal(200, status);
        Assert.Equal(11, categorias.GetArrayLength());
        Assert.Equal("Mercearia", categorias[0].GetProperty("nome").GetString());
        Assert.Equal("Outros", categorias[10].GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Rotas_protegidas_sem_token_respondem_401()
    {
        var anonimo = _api.CreateClient();

        Assert.Equal(401, (int)(await anonimo.GetAsync("/listas")).StatusCode);
        Assert.Equal(401, (int)(await anonimo.GetAsync("/categorias")).StatusCode);
    }

    [PostgresFact]
    public async Task Alterar_a_lista_padrao_nao_muda_listas_ja_criadas()
    {
        var u = await _api.NovoUsuarioAsync();
        var arroz = await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var feijao = await AdicionarAoModeloAsync(u, "Feijão", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        Assert.Equal(["Arroz", "Feijão"], Nomes(lista.GetProperty("itens")));

        await u.PutAsync($"/modelo/itens/{arroz}", new { nome = "Arroz integral", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Kg", quantidade = 9 });
        await u.DeleteAsync($"/modelo/itens/{feijao}");

        var (_, depois) = await u.GetAsync($"/listas/{listaId}");
        var itens = depois.GetProperty("itens");
        Assert.Equal(["Arroz", "Feijão"], Nomes(itens));
        Assert.Equal(2, itens[0].GetProperty("quantidade").GetDecimal());
        Assert.Equal("Pacote", itens[0].GetProperty("unidade").GetString());

        var (_, modelo) = await u.GetAsync("/modelo/itens");
        Assert.Equal(["Arroz integral"], Nomes(modelo));
    }

    [PostgresFact]
    public async Task Lista_criada_da_padrao_pode_levar_so_os_itens_selecionados()
    {
        var u = await _api.NovoUsuarioAsync();
        var arroz = await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        await AdicionarAoModeloAsync(u, "Leite", "Laticínios", "Litro", 6);

        var lista = await CriarListaAsync(u, "2026-12", itensDoModelo: [arroz]);

        Assert.Equal(["Arroz"], Nomes(lista.GetProperty("itens")));
    }

    [PostgresFact]
    public async Task Adicionar_ou_remover_na_lista_do_mes_nao_mexe_na_lista_padrao()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var arrozNaLista = lista.GetProperty("itens")[0].GetProperty("id").GetGuid();

        var (statusAdd, _) = await u.PostAsync($"/listas/{listaId}/itens", new { nome = "Sorvete", categoriaId = await u.CategoriaAsync("Congelados"), unidade = "Caixa", quantidade = 1 });
        var (statusDel, _) = await u.DeleteAsync($"/listas/{listaId}/itens/{arrozNaLista}");

        Assert.Equal(201, statusAdd);
        Assert.Equal(204, statusDel);
        var (_, modelo) = await u.GetAsync("/modelo/itens");
        Assert.Equal(["Arroz"], Nomes(modelo));
        var (_, atual) = await u.GetAsync($"/listas/{listaId}");
        Assert.Equal(["Sorvete"], Nomes(atual.GetProperty("itens")));
    }

    [PostgresFact]
    public async Task Segunda_lista_principal_no_mes_exige_confirmacao()
    {
        var u = await _api.NovoUsuarioAsync();
        await CriarListaAsync(u, "2026-12", "Zero");

        var (status, erro) = await u.PostAsync("/listas", new { competencia = "2026-12", origem = "Zero", permitirDuplicada = false });
        Assert.Equal(422, status);
        Assert.Contains("lista principal", erro.GetProperty("detail").GetString());

        var segunda = await CriarListaAsync(u, "2026-12", "Zero", permitirDuplicada: true, nome: "Festa de fim de ano");
        Assert.False(segunda.GetProperty("resumo").GetProperty("principal").GetBoolean());
        Assert.Equal("Festa de fim de ano", segunda.GetProperty("resumo").GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Aceita_mes_futuro_e_passado_mas_recusa_formato_invalido()
    {
        var u = await _api.NovoUsuarioAsync();

        await CriarListaAsync(u, "2030-01", "Zero");
        await CriarListaAsync(u, "2020-05", "Zero");
        var (status, _) = await u.PostAsync("/listas", new { competencia = "2026-13", origem = "Zero" });

        Assert.Equal(422, status);
        var (_, listas) = await u.GetAsync("/listas");
        Assert.Equal(["2030-01", "2020-05"], listas.EnumerateArray().Select(l => l.GetProperty("competencia").GetString()!).ToArray());
    }

    [PostgresFact]
    public async Task Marcar_comprado_atualiza_progresso_e_registra_quem_marcou()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        await AdicionarAoModeloAsync(u, "Leite", "Laticínios", "Litro", 6);
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var itemId = lista.GetProperty("itens")[0].GetProperty("id").GetGuid();

        var (status, item) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });

        Assert.Equal(200, status);
        Assert.True(item.GetProperty("comprado").GetBoolean());
        Assert.Equal(u.Id, item.GetProperty("compradoPorUsuarioId").GetGuid());
        var (_, atual) = await u.GetAsync($"/listas/{listaId}");
        Assert.Equal(50, atual.GetProperty("resumo").GetProperty("percentual").GetInt32());
        Assert.Equal(1, atual.GetProperty("resumo").GetProperty("comprados").GetInt32());

        await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = false });
        var (_, desmarcado) = await u.GetAsync($"/listas/{listaId}");
        Assert.Equal(0, desmarcado.GetProperty("resumo").GetProperty("percentual").GetInt32());
    }

    [PostgresFact]
    public async Task Lista_finalizada_e_somente_leitura_ate_ser_reaberta()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var itemId = lista.GetProperty("itens")[0].GetProperty("id").GetGuid();

        var (statusFinalizar, resumo) = await u.PostAsync($"/listas/{listaId}/finalizar");
        Assert.Equal(200, statusFinalizar);
        Assert.Equal("Finalizada", resumo.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, resumo.GetProperty("finalizadaEm").ValueKind);

        var (statusMarcar, erro) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });
        Assert.Equal(422, statusMarcar);
        Assert.Contains("finalizada", erro.GetProperty("detail").GetString());
        Assert.Equal(422, (await u.DeleteAsync($"/listas/{listaId}/itens/{itemId}")).Status);

        Assert.Equal(200, (await u.PostAsync($"/listas/{listaId}/reabrir")).Status);
        Assert.Equal(200, (await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true })).Status);
    }

    [PostgresFact]
    public async Task Cancelar_apaga_a_lista_em_andamento_do_historico_e_recusa_finalizada_e_de_outro_grupo()
    {
        var u = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();

        Assert.Equal(404, (await intruso.DeleteAsync($"/listas/{listaId}")).Status);
        Assert.Equal(204, (await u.DeleteAsync($"/listas/{listaId}")).Status);
        Assert.Equal(404, (await u.GetAsync($"/listas/{listaId}")).Status);
        Assert.Equal(0, (await u.GetAsync("/listas")).Corpo.GetArrayLength());
        Assert.Equal(1, (await u.GetAsync("/modelo/itens")).Corpo.GetArrayLength());

        // Depois de cancelar, o mês pode ser iniciado de novo como principal.
        var recriada = await CriarListaAsync(u, "2026-12");
        var novaId = recriada.GetProperty("resumo").GetProperty("id").GetGuid();
        await u.PostAsync($"/listas/{novaId}/finalizar");
        Assert.Equal(422, (await u.DeleteAsync($"/listas/{novaId}")).Status);
    }

    [PostgresFact]
    public async Task Usuario_nao_enxerga_nem_altera_dados_de_outro_grupo()
    {
        var dona = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(dona, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(dona, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var itemId = lista.GetProperty("itens")[0].GetProperty("id").GetGuid();

        Assert.Equal(404, (await intruso.GetAsync($"/listas/{listaId}")).Status);
        Assert.Equal(404, (await intruso.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true })).Status);
        Assert.Equal(404, (await intruso.PostAsync($"/listas/{listaId}/finalizar")).Status);
        Assert.Equal(0, (await intruso.GetAsync("/listas")).Corpo.GetArrayLength());
        Assert.Equal(0, (await intruso.GetAsync("/modelo/itens")).Corpo.GetArrayLength());

        var (_, aindaIntacta) = await dona.GetAsync($"/listas/{listaId}");
        Assert.False(aindaIntacta.GetProperty("itens")[0].GetProperty("comprado").GetBoolean());
    }

    [PostgresFact]
    public async Task Edicao_com_versao_antiga_e_recusada_por_conflito()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var item = lista.GetProperty("itens")[0];
        var itemId = item.GetProperty("id").GetGuid();
        var categoria = await u.CategoriaAsync("Mercearia");
        var versaoLida = item.GetProperty("versao").GetInt32();

        var (primeira, editado) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 3, versao = versaoLida });
        var (segunda, erro) = await u.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 7, versao = versaoLida });

        Assert.Equal(200, primeira);
        Assert.Equal(versaoLida + 1, editado.GetProperty("versao").GetInt32());
        Assert.Equal(409, segunda);
        Assert.Contains("outra pessoa", erro.GetProperty("detail").GetString());
        var (_, atual) = await u.GetAsync($"/listas/{listaId}");
        Assert.Equal(3, atual.GetProperty("itens")[0].GetProperty("quantidade").GetDecimal());
    }

    [PostgresFact]
    public async Task Renomear_e_excluir_categoria_preserva_o_historico()
    {
        var u = await _api.NovoUsuarioAsync();
        var bebidas = await u.CategoriaAsync("Bebidas");
        var outros = await u.CategoriaAsync("Outros");
        await AdicionarAoModeloAsync(u, "Suco", "Bebidas", "Litro", 2);
        var lista = await CriarListaAsync(u, "2026-12");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();

        Assert.Equal(200, (await u.PutAsync($"/categorias/{bebidas}", new { nome = "Bebidas geladas" })).Status);
        Assert.Equal(204, (await u.DeleteAsync($"/categorias/{bebidas}")).Status);

        var (_, atual) = await u.GetAsync($"/listas/{listaId}");
        Assert.Equal("Bebidas", atual.GetProperty("itens")[0].GetProperty("categoriaNome").GetString());

        var (_, modelo) = await u.GetAsync("/modelo/itens");
        Assert.Equal(outros, modelo[0].GetProperty("categoriaId").GetGuid());
        var (_, categorias) = await u.GetAsync("/categorias");
        Assert.Equal(10, categorias.GetArrayLength());

        var (status, _) = await u.PostAsync($"/listas/{listaId}/itens", new { nome = "Cerveja", categoriaId = bebidas, unidade = "Unidade", quantidade = 6 });
        Assert.Equal(400, status);
    }

    [PostgresFact]
    public async Task Nao_cria_produto_duplicado_ao_reutilizar_o_mesmo_nome()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var lista = await CriarListaAsync(u, "2026-12", "Zero");
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();

        await u.PostAsync($"/listas/{listaId}/itens", new { nome = "  arroz ", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 1 });

        var (_, produtos) = await u.GetAsync("/produtos?q=arr");
        Assert.Equal(["Arroz"], Nomes(produtos));
    }

    [PostgresFact]
    public async Task Reordena_a_lista_padrao_e_as_listas_novas_seguem_a_ordem()
    {
        var u = await _api.NovoUsuarioAsync();
        var a = await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        var b = await AdicionarAoModeloAsync(u, "Banana", "Hortifruti", "Kg", 1);
        var c = await AdicionarAoModeloAsync(u, "Café", "Mercearia");

        Assert.Equal(204, (await u.PutAsync("/modelo/itens/ordem", new { ids = new[] { c, a, b } })).Status);

        var (_, modelo) = await u.GetAsync("/modelo/itens");
        Assert.Equal(["Café", "Arroz", "Banana"], Nomes(modelo));
        var lista = await CriarListaAsync(u, "2026-12");
        Assert.Equal(["Café", "Arroz", "Banana"], Nomes(lista.GetProperty("itens")));
        Assert.Equal(400, (await u.PutAsync("/modelo/itens/ordem", new { ids = new[] { c, a } })).Status);
    }

    [PostgresFact]
    public async Task Busca_na_lista_padrao_ignora_maiusculas()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Arroz", "Mercearia");
        await AdicionarAoModeloAsync(u, "Leite", "Laticínios", "Litro", 6);

        var (_, achados) = await u.GetAsync("/modelo/itens?q=ARR");

        Assert.Equal(["Arroz"], Nomes(achados));
    }

    [PostgresFact]
    public async Task Valida_quantidade_nome_e_unidade()
    {
        var u = await _api.NovoUsuarioAsync();
        var categoria = await u.CategoriaAsync("Mercearia");

        Assert.Equal(400, (await u.PostAsync("/modelo/itens", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 0 })).Status);
        Assert.Equal(400, (await u.PostAsync("/modelo/itens", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 1.2345 })).Status);
        Assert.Equal(400, (await u.PostAsync("/modelo/itens", new { nome = "   ", categoriaId = categoria, unidade = "Pacote", quantidade = 1 })).Status);
        Assert.Equal(400, (await u.PostAsync("/modelo/itens", new { nome = "Arroz", categoriaId = categoria, unidade = "Barril", quantidade = 1 })).Status);
        Assert.Equal(400, (await u.PostAsync("/modelo/itens", new { nome = "Arroz", categoriaId = Guid.NewGuid(), unidade = "Pacote", quantidade = 1 })).Status);
    }

    [PostgresFact]
    public async Task Quantidade_fracionada_e_preservada_com_tres_casas()
    {
        var u = await _api.NovoUsuarioAsync();
        await AdicionarAoModeloAsync(u, "Queijo", "Laticínios", "Kg", 0.333m);

        var lista = await CriarListaAsync(u, "2026-12");

        Assert.Equal(0.333m, lista.GetProperty("itens")[0].GetProperty("quantidade").GetDecimal());
    }
}
