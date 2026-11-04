using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class MapasEComprasDivididasTests
{
    private readonly ApiFactory _api;

    public MapasEComprasDivididasTests(ApiFactory api)
    {
        _api = api;
    }

    private static object Area(int x, int y, int largura, int altura) => new { x, y, largura, altura };

    private static object Secao(string nome, object area, Guid[]? ladoA = null, Guid[]? ladoB = null, string? nomeLadoB = null, bool dividida = false)
    {
        return new { nome, nomeLadoB, dividida, area, categoriasLadoA = ladoA ?? [], categoriasLadoB = ladoB ?? [] };
    }

    private static object Mapa(string nome, int versao, params object[] secoes)
    {
        return new { nome, largura = 30, altura = 20, entrada = new { x = 2, y = 18 }, caixas = Area(24, 17, 5, 2), secoes, versao };
    }

    private static async Task<JsonElement> NovaLojaAsync(UsuarioDeTeste u, string nome)
    {
        var (status, loja) = await u.PostAsync("/lojas", new { nome });
        Assert.Equal(201, status);
        return loja;
    }

    private static async Task<Guid> NovaListaAsync(UsuarioDeTeste u)
    {
        var (_, lista) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero" });
        return lista.GetProperty("resumo").GetProperty("id").GetGuid();
    }

    private static async Task<Guid> NovoItemAsync(UsuarioDeTeste u, Guid lista, string nome, string categoria)
    {
        var (_, item) = await u.PostAsync($"/listas/{lista}/itens", new { nome, categoriaId = await u.CategoriaAsync(categoria), unidade = "Unidade", quantidade = 1 });
        return item.GetProperty("id").GetGuid();
    }

    /// <summary>Mercado com Mercearia e Limpeza (lados diferentes de uma gôndola dividida) e Açougue numa seção simples.</summary>
    private static async Task<Guid> LojaMapeadaAsync(UsuarioDeTeste u, string nome)
    {
        var loja = await NovaLojaAsync(u, nome);
        var mercearia = await u.CategoriaAsync("Mercearia");
        var limpeza = await u.CategoriaAsync("Limpeza");
        var acougue = await u.CategoriaAsync("Açougue");
        var (status, salvo) = await u.PutAsync($"/lojas/{loja.GetProperty("id").GetGuid()}/mapa", Mapa(nome, loja.GetProperty("versao").GetInt32(),
            Secao("Mercearia", Area(8, 8, 10, 2), [mercearia], [limpeza], "Limpeza", dividida: true),
            Secao("Açougue", Area(3, 3, 8, 2), [acougue])));
        Assert.Equal(200, status);
        return salvo.GetProperty("id").GetGuid();
    }

    [PostgresFact]
    public async Task Cria_mercado_em_branco_e_salva_o_mapa_com_as_categorias_de_cada_lado()
    {
        var u = await _api.NovoUsuarioAsync();
        var lojaId = await LojaMapeadaAsync(u, "Mercado Central");

        var (status, loja) = await u.GetAsync($"/lojas/{lojaId}");

        Assert.Equal(200, status);
        Assert.Equal(2, loja.GetProperty("versao").GetInt32());
        var gondola = loja.GetProperty("secoes").EnumerateArray().First(s => s.GetProperty("dividida").GetBoolean());
        Assert.Equal("Limpeza", gondola.GetProperty("nomeLadoB").GetString());
        Assert.Equal(await u.CategoriaAsync("Mercearia"), gondola.GetProperty("categoriasLadoA")[0].GetGuid());
        Assert.Equal(await u.CategoriaAsync("Limpeza"), gondola.GetProperty("categoriasLadoB")[0].GetGuid());
        Assert.Equal(1, (await u.GetAsync("/lojas")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Mapa_com_secoes_sobrepostas_ou_fora_da_grade_e_recusado()
    {
        var u = await _api.NovoUsuarioAsync();
        var loja = await NovaLojaAsync(u, "Teste");
        var id = loja.GetProperty("id").GetGuid();

        Assert.Equal(400, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("A", Area(5, 5, 6, 2)), Secao("B", Area(8, 5, 6, 2))))).Status);
        Assert.Equal(400, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("A", Area(28, 5, 6, 2))))).Status);
        Assert.Equal(400, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("Em cima dos caixas", Area(25, 17, 2, 2))))).Status);
        Assert.Equal(400, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("Fina", Area(5, 5, 8, 1), nomeLadoB: "B", dividida: true)))).Status);
        Assert.Equal(400, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("Categoria falsa", Area(5, 5, 6, 2), [Guid.NewGuid()])))).Status);
    }

    [PostgresFact]
    public async Task Salvar_mapa_com_versao_velha_da_conflito()
    {
        var u = await _api.NovoUsuarioAsync();
        var loja = await NovaLojaAsync(u, "Teste");
        var id = loja.GetProperty("id").GetGuid();
        Assert.Equal(200, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("A", Area(5, 5, 6, 2))))).Status);

        Assert.Equal(409, (await u.PutAsync($"/lojas/{id}/mapa", Mapa("Teste", 1, Secao("A", Area(5, 5, 6, 2))))).Status);
    }

    [PostgresFact]
    public async Task Mercado_de_outro_grupo_nao_e_visivel_nem_editavel()
    {
        var dona = await _api.NovoUsuarioAsync();
        var intruso = await _api.NovoUsuarioAsync();
        var lojaId = await LojaMapeadaAsync(dona, "Particular");

        Assert.Equal(404, (await intruso.GetAsync($"/lojas/{lojaId}")).Status);
        Assert.Equal(404, (await intruso.PutAsync($"/lojas/{lojaId}/mapa", Mapa("X", 2))).Status);
        Assert.Equal(404, (await intruso.DeleteAsync($"/lojas/{lojaId}")).Status);
        Assert.Equal(0, (await intruso.GetAsync("/lojas")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Copiar_mercado_leva_o_layout_com_ids_novos()
    {
        var u = await _api.NovoUsuarioAsync();
        var origem = await LojaMapeadaAsync(u, "Original");

        var (status, copia) = await u.PostAsync("/lojas", new { nome = "Copia", copiarDeLojaId = origem });

        Assert.Equal(201, status);
        Assert.Equal(2, copia.GetProperty("secoes").GetArrayLength());
        Assert.NotEqual(origem, copia.GetProperty("id").GetGuid());
        Assert.Equal(409, (await u.PostAsync("/lojas", new { nome = "copia" })).Status);
    }

    [PostgresFact]
    public async Task Compra_dividida_a_rota_do_segundo_mercado_so_tem_o_que_falta()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercadoA = await LojaMapeadaAsync(u, "Mercado A");
        var mercadoB = await LojaMapeadaAsync(u, "Mercado B");
        var lista = await NovaListaAsync(u);
        var arroz = await NovoItemAsync(u, lista, "Arroz", "Mercearia");
        await NovoItemAsync(u, lista, "Detergente", "Limpeza");
        await NovoItemAsync(u, lista, "Frango", "Açougue");

        var (statusMarca, marcado) = await u.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = true, lojaId = mercadoA });
        var (statusRota, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercadoB });

        Assert.Equal(200, statusMarca);
        Assert.Equal(mercadoA, marcado.GetProperty("compradoNaLojaId").GetGuid());
        Assert.Equal(200, statusRota);
        var nomes = rota.GetProperty("paradas").EnumerateArray().SelectMany(p => p.GetProperty("itens").EnumerateArray()).Select(i => i.GetProperty("nome").GetString()).ToArray();
        Assert.Equivalent(new[] { "Detergente", "Frango" }, nomes);
        Assert.Equal(1, rota.GetProperty("jaCompradosEmOutrosMercados").GetInt32());
        Assert.True(rota.GetProperty("distanciaEmMetros").GetInt32() > 0);
    }

    [PostgresFact]
    public async Task Item_do_lado_B_da_gondola_vem_com_o_texto_do_lado()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var lista = await NovaListaAsync(u);
        await NovoItemAsync(u, lista, "Detergente", "Limpeza");

        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });

        var parada = rota.GetProperty("paradas")[0];
        Assert.Equal("Limpeza", parada.GetProperty("nome").GetString());
        Assert.Equal("B", parada.GetProperty("lado").GetString());
        Assert.Equal("lado de baixo", parada.GetProperty("ladoTexto").GetString());
    }

    [PostgresFact]
    public async Task Divisao_por_categoria_tira_da_rota_o_que_outra_pessoa_pega_e_o_resto_fica_para_todos()
    {
        var eu = await _api.NovoUsuarioAsync();
        var esposa = await _api.NovoUsuarioAsync();
        var (_, convite) = await eu.PostAsync("/grupo/convites");
        var (_, sessao) = await esposa.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        esposa.AplicarSessao(sessao);

        var mercado = await LojaMapeadaAsync(eu, "Mercado A");
        var lista = await NovaListaAsync(eu);
        await NovoItemAsync(eu, lista, "Frango", "Açougue");
        await NovoItemAsync(eu, lista, "Arroz", "Mercearia");
        var acougue = await eu.CategoriaAsync("Açougue");

        // Sem divisão, a rota (de qualquer pessoa) leva tudo.
        var (_, semDivisao) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado, usuarioId = esposa.Id });
        Assert.Equal(2, semDivisao.GetProperty("paradas").EnumerateArray().Sum(p => p.GetProperty("itens").GetArrayLength()));

        var (status, divisao) = await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = acougue, usuarioId = esposa.Id } } });
        Assert.Equal(200, status);
        Assert.Equal(1, divisao.GetArrayLength());

        // Minha rota: só o arroz (categoria de todos); o frango está com ela.
        var (_, minha) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado, usuarioId = eu.Id });
        var nomesMinha = minha.GetProperty("paradas").EnumerateArray().SelectMany(p => p.GetProperty("itens").EnumerateArray()).Select(i => i.GetProperty("nome").GetString()).ToList();
        Assert.Equal(["Arroz"], nomesMinha);
        Assert.Equal("Frango", minha.GetProperty("comOutrasPessoas")[0].GetProperty("item").GetProperty("nome").GetString());
        Assert.Equal(esposa.Id, minha.GetProperty("comOutrasPessoas")[0].GetProperty("usuarioId").GetGuid());

        // A rota dela leva o frango e o arroz; a rota sem pessoa (todos) leva os dois.
        var (_, dela) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado, usuarioId = esposa.Id });
        Assert.Equal(2, dela.GetProperty("paradas").EnumerateArray().Sum(p => p.GetProperty("itens").GetArrayLength()));
        var (_, todos) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });
        Assert.Equal(2, todos.GetProperty("paradas").EnumerateArray().Sum(p => p.GetProperty("itens").GetArrayLength()));
        Assert.Equal(0, todos.GetProperty("comOutrasPessoas").GetArrayLength());

        // A divisão volta na lista e some ao desfazer; só vale para esta lista.
        Assert.Equal(1, (await eu.GetAsync($"/listas/{lista}")).Corpo.GetProperty("divisao").GetArrayLength());
        var (_, criada) = await eu.PostAsync("/listas", new { competencia = "2026-12", origem = "Zero" });
        var outraLista = criada.GetProperty("resumo").GetProperty("id").GetGuid();
        Assert.Equal(0, (await eu.GetAsync($"/listas/{outraLista}")).Corpo.GetProperty("divisao").GetArrayLength());
        await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = Array.Empty<object>() });
        Assert.Equal(0, (await eu.GetAsync($"/listas/{lista}")).Corpo.GetProperty("divisao").GetArrayLength());
    }

    [PostgresFact]
    public async Task Rota_mostra_onde_a_outra_pessoa_esta_e_o_trajeto_que_falta_para_ela()
    {
        var eu = await _api.NovoUsuarioAsync();
        var esposa = await _api.NovoUsuarioAsync();
        var (_, convite) = await eu.PostAsync("/grupo/convites");
        var (_, sessao) = await esposa.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        esposa.AplicarSessao(sessao);

        var mercado = await LojaMapeadaAsync(eu, "Mercado A");
        var acougue = await SecaoDoMercadoAsync(eu, mercado, "Açougue");
        var lista = await NovaListaAsync(eu);
        var frango = await NovoItemAsync(eu, lista, "Frango", "Açougue");
        await NovoItemAsync(eu, lista, "Arroz", "Mercearia");
        await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = await eu.CategoriaAsync("Açougue"), usuarioId = esposa.Id } } });

        // Sem ter marcado nada, ela está na entrada e ainda falta o frango e o arroz (de todos).
        var (_, antes) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado, usuarioId = eu.Id });
        var antesDela = Assert.Single(antes.GetProperty("rotasDeOutrasPessoas").EnumerateArray());
        Assert.Equal(esposa.Id, antesDela.GetProperty("usuarioId").GetGuid());
        Assert.Equal(2, antesDela.GetProperty("itensRestantes").GetInt32());
        Assert.Equal(2, antesDela.GetProperty("posicao").GetProperty("x").GetInt32());

        // Depois de marcar o frango, ela está no açougue e só falta o arroz.
        await esposa.PutAsync($"/listas/{lista}/itens/{frango}/comprado", new { comprado = true, lojaId = mercado });
        var (_, depois) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado, usuarioId = eu.Id });
        var depoisDela = Assert.Single(depois.GetProperty("rotasDeOutrasPessoas").EnumerateArray());
        Assert.Equal(1, depoisDela.GetProperty("itensRestantes").GetInt32());
        var pos = depoisDela.GetProperty("posicao");
        Assert.InRange(pos.GetProperty("x").GetInt32(), 3, 11);
        Assert.InRange(pos.GetProperty("y").GetInt32(), 2, 6);
        Assert.True(depoisDela.GetProperty("trilha").GetArrayLength() >= 2);

        // Sem escolher uma pessoa (rota de todos) ou sem divisão, não há outras rotas.
        var (_, todos) = await eu.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });
        Assert.Equal(0, todos.GetProperty("rotasDeOutrasPessoas").GetArrayLength());
        Assert.Equal(acougue, await SecaoDoMercadoAsync(eu, mercado, "Açougue"));
    }

    [PostgresFact]
    public async Task Divisao_recusa_pessoa_de_fora_do_grupo_categoria_repetida_e_lista_de_outro_grupo()
    {
        var eu = await _api.NovoUsuarioAsync();
        var estranho = await _api.NovoUsuarioAsync();
        var lista = await NovaListaAsync(eu);
        var acougue = await eu.CategoriaAsync("Açougue");
        var mercearia = await eu.CategoriaAsync("Mercearia");

        Assert.Equal(400, (await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = acougue, usuarioId = estranho.Id } } })).Status);
        Assert.Equal(400, (await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = acougue, usuarioId = eu.Id }, new { categoriaId = acougue, usuarioId = eu.Id } } })).Status);
        Assert.Equal(400, (await eu.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = Guid.NewGuid(), usuarioId = eu.Id } } })).Status);
        Assert.Equal(404, (await estranho.PutAsync($"/listas/{lista}/divisao", new { atribuicoes = new[] { new { categoriaId = mercearia, usuarioId = estranho.Id } } })).Status);
    }

    [PostgresFact]
    public async Task Nao_tem_aqui_tira_o_item_da_rota_do_mercado_mas_ele_continua_pendente()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercadoA = await LojaMapeadaAsync(u, "Mercado A");
        var mercadoB = await LojaMapeadaAsync(u, "Mercado B");
        var lista = await NovaListaAsync(u);
        var frango = await NovoItemAsync(u, lista, "Frango", "Açougue");

        var (status, item) = await u.PutAsync($"/listas/{lista}/itens/{frango}/sem-na-loja", new { lojaId = mercadoA, sem = true });
        var (_, rotaA) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercadoA });
        var (_, rotaB) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercadoB });

        Assert.Equal(200, status);
        Assert.False(item.GetProperty("comprado").GetBoolean());
        Assert.Equal(0, rotaA.GetProperty("paradas").GetArrayLength());
        Assert.Equal("NaoTemNaLoja", rotaA.GetProperty("foraDaRota")[0].GetProperty("motivo").GetString());
        Assert.Equal(1, rotaB.GetProperty("paradas").GetArrayLength());

        await u.PutAsync($"/listas/{lista}/itens/{frango}/sem-na-loja", new { lojaId = mercadoA, sem = false });
        Assert.Equal(1, (await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercadoA })).Corpo.GetProperty("paradas").GetArrayLength());
    }

    [PostgresFact]
    public async Task Categoria_sem_secao_no_mercado_fica_fora_da_rota_e_pendente()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var lista = await NovaListaAsync(u);
        await NovoItemAsync(u, lista, "Pão", "Padaria");

        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });

        Assert.Equal(0, rota.GetProperty("paradas").GetArrayLength());
        Assert.Equal("SemSecao", rota.GetProperty("foraDaRota")[0].GetProperty("motivo").GetString());
    }

    private static async Task<Guid> SecaoDoMercadoAsync(UsuarioDeTeste u, Guid mercado, string nome)
    {
        var (_, loja) = await u.GetAsync($"/lojas/{mercado}");
        return loja.GetProperty("secoes").EnumerateArray().First(s => s.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();
    }

    [PostgresFact]
    public async Task Produto_tem_um_unico_local_por_mercado_e_marcar_outro_descarta_o_anterior()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var acougue = await SecaoDoMercadoAsync(u, mercado, "Açougue");
        var mercearia = await SecaoDoMercadoAsync(u, mercado, "Mercearia");
        var lista = await NovaListaAsync(u);
        var pao = await NovoItemAsync(u, lista, "Pão", "Padaria");

        await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = acougue, lado = "A" } } });
        Assert.Equal(200, (await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = mercearia, lado = "A" } } })).Status);

        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });
        var proprios = rota.GetProperty("locaisProprios");
        Assert.Equal(1, proprios.GetArrayLength());
        Assert.Equal(mercearia, proprios[0].GetProperty("secaoId").GetGuid());

        // Dois de uma vez é recusado e o local que já existia continua.
        var (status, _) = await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = acougue, lado = "A" }, new { secaoId = mercearia, lado = "A" } } });
        Assert.Equal(400, status);
        var (_, depois) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });
        Assert.Equal(mercearia, depois.GetProperty("locaisProprios")[0].GetProperty("secaoId").GetGuid());
    }

    [PostgresFact]
    public async Task Local_marcado_pelo_usuario_leva_a_rota_para_la_e_vale_para_listas_futuras()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var acougue = await SecaoDoMercadoAsync(u, mercado, "Açougue");
        var lista = await NovaListaAsync(u);
        var pao = await NovoItemAsync(u, lista, "Pão", "Padaria");

        var (status, locais) = await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = acougue, lado = "A" } } });
        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });

        Assert.Equal(200, status);
        Assert.Equal(1, locais.GetArrayLength());
        Assert.Equal("Açougue", rota.GetProperty("paradas")[0].GetProperty("nome").GetString());
        Assert.Equal(0, rota.GetProperty("foraDaRota").GetArrayLength());
        Assert.Equal(acougue, rota.GetProperty("locaisProprios")[0].GetProperty("secaoId").GetGuid());

        // Outro mês, mesmo produto escrito de outro jeito: o local do grupo já vale.
        var (_, outra) = await u.PostAsync("/listas", new { competencia = "2026-12", origem = "Zero" });
        var proxima = outra.GetProperty("resumo").GetProperty("id").GetGuid();
        await NovoItemAsync(u, proxima, "PÃO", "Padaria");
        var (_, rotaDoMes) = await u.PostAsync($"/listas/{proxima}/rota", new { lojaId = mercado });
        Assert.Equal("Açougue", rotaDoMes.GetProperty("paradas")[0].GetProperty("nome").GetString());

        // Lista vazia apaga: volta a valer a categoria (que este mercado não tem).
        await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = Array.Empty<object>() });
        var (_, semLocal) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });
        Assert.Equal("SemSecao", semLocal.GetProperty("foraDaRota")[0].GetProperty("motivo").GetString());
    }

    [PostgresFact]
    public async Task Local_do_item_recusa_secao_de_outro_mercado_e_lado_B_de_secao_simples()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var outro = await LojaMapeadaAsync(u, "Mercado B");
        var lista = await NovaListaAsync(u);
        var item = await NovoItemAsync(u, lista, "Pão", "Padaria");

        var secaoDoOutro = await SecaoDoMercadoAsync(u, outro, "Açougue");
        var (deOutro, _) = await u.PutAsync($"/listas/{lista}/itens/{item}/local", new { lojaId = mercado, locais = new[] { new { secaoId = secaoDoOutro, lado = "A" } } });
        var simples = await SecaoDoMercadoAsync(u, mercado, "Açougue");
        var (ladoB, _) = await u.PutAsync($"/listas/{lista}/itens/{item}/local", new { lojaId = mercado, locais = new[] { new { secaoId = simples, lado = "B" } } });

        Assert.Equal(400, deOutro);
        Assert.Equal(400, ladoB);
    }

    [PostgresFact]
    public async Task Salvar_o_mapa_de_novo_mantem_o_local_da_secao_que_continua_e_descarta_o_da_que_saiu()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var acougue = await SecaoDoMercadoAsync(u, mercado, "Açougue");
        var mercearia = await SecaoDoMercadoAsync(u, mercado, "Mercearia");
        var lista = await NovaListaAsync(u);
        var pao = await NovoItemAsync(u, lista, "Pão", "Padaria");
        var leite = await NovoItemAsync(u, lista, "Leite", "Laticínios");
        await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = acougue, lado = "A" } } });
        await u.PutAsync($"/listas/{lista}/itens/{leite}/local", new { lojaId = mercado, locais = new[] { new { secaoId = mercearia, lado = "A" } } });

        // Reenvia o mapa: o Açougue continua (e muda de lugar); a Mercearia sai.
        var (_, loja) = await u.GetAsync($"/lojas/{mercado}");
        var (status, salvo) = await u.PutAsync($"/lojas/{mercado}/mapa", new
        {
            nome = "Mercado A", largura = 30, altura = 20, entrada = new { x = 2, y = 18 }, caixas = Area(24, 17, 5, 2), versao = loja.GetProperty("versao").GetInt32(),
            secoes = new[] { new { nome = "Açougue", nomeLadoB = (string?)null, dividida = false, area = Area(12, 3, 8, 2), categoriasLadoA = Array.Empty<Guid>(), categoriasLadoB = Array.Empty<Guid>(), idAnterior = (Guid?)acougue } },
        });
        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = mercado });

        Assert.Equal(200, status);
        var novoAcougue = salvo.GetProperty("secoes")[0].GetProperty("id").GetGuid();
        var proprios = rota.GetProperty("locaisProprios").EnumerateArray().ToList();
        Assert.Single(proprios);
        Assert.Equal(novoAcougue, proprios[0].GetProperty("secaoId").GetGuid());
        Assert.Equal(pao, proprios[0].GetProperty("itemId").GetGuid());
    }

    [PostgresFact]
    public async Task Copiar_mercado_leva_tambem_os_locais_dos_produtos()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var acougue = await SecaoDoMercadoAsync(u, mercado, "Açougue");
        var lista = await NovaListaAsync(u);
        var pao = await NovoItemAsync(u, lista, "Pão", "Padaria");
        await u.PutAsync($"/listas/{lista}/itens/{pao}/local", new { lojaId = mercado, locais = new[] { new { secaoId = acougue, lado = "A" } } });

        var (_, copia) = await u.PostAsync("/lojas", new { nome = "Mercado B", copiarDeLojaId = mercado });
        var (_, rota) = await u.PostAsync($"/listas/{lista}/rota", new { lojaId = copia.GetProperty("id").GetGuid() });

        Assert.Equal("Açougue", rota.GetProperty("paradas")[0].GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Desmarcar_item_limpa_o_mercado_da_compra()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var lista = await NovaListaAsync(u);
        var arroz = await NovoItemAsync(u, lista, "Arroz", "Mercearia");
        await u.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = true, lojaId = mercado });

        var (_, desmarcado) = await u.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = false });

        Assert.False(desmarcado.GetProperty("comprado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, desmarcado.GetProperty("compradoNaLojaId").ValueKind);
    }

    [PostgresFact]
    public async Task Marcar_comprado_exige_mercado_do_proprio_grupo()
    {
        var dona = await _api.NovoUsuarioAsync();
        var outra = await _api.NovoUsuarioAsync();
        var lojaDeOutra = await LojaMapeadaAsync(outra, "De outra");
        var lista = await NovaListaAsync(dona);
        var arroz = await NovoItemAsync(dona, lista, "Arroz", "Mercearia");

        Assert.Equal(400, (await dona.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = true, lojaId = lojaDeOutra })).Status);
        Assert.Equal(400, (await dona.PutAsync($"/listas/{lista}/itens/{arroz}/sem-na-loja", new { lojaId = lojaDeOutra, sem = true })).Status);
        Assert.Equal(404, (await dona.PostAsync($"/listas/{lista}/rota", new { lojaId = lojaDeOutra })).Status);
    }

    [PostgresFact]
    public async Task Excluir_mercado_preserva_onde_os_itens_antigos_foram_comprados()
    {
        var u = await _api.NovoUsuarioAsync();
        var mercado = await LojaMapeadaAsync(u, "Mercado A");
        var lista = await NovaListaAsync(u);
        var arroz = await NovoItemAsync(u, lista, "Arroz", "Mercearia");
        await u.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = true, lojaId = mercado });

        Assert.Equal(204, (await u.DeleteAsync($"/lojas/{mercado}")).Status);

        Assert.Equal(0, (await u.GetAsync("/lojas")).Corpo.GetArrayLength());
        Assert.Equal(404, (await u.GetAsync($"/lojas/{mercado}")).Status);
        var (_, depois) = await u.GetAsync($"/listas/{lista}");
        Assert.Equal(mercado, depois.GetProperty("itens")[0].GetProperty("compradoNaLojaId").GetGuid());
        Assert.Equal(400, (await u.PutAsync($"/listas/{lista}/itens/{arroz}/comprado", new { comprado = true, lojaId = mercado })).Status);
    }
}
