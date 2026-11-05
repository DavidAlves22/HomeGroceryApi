using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class PainelAdminTests
{
    private readonly ApiFactory _api;

    public PainelAdminTests(ApiFactory api)
    {
        _api = api;
    }

    /// <summary>O administrador é um só: o e-mail da configuração. Se outro teste já o cadastrou, entra pelo login.</summary>
    private async Task<UsuarioDeTeste> AdminAsync()
    {
        var cliente = _api.CreateClient();
        var login = await cliente.PostAsJsonAsync("/autenticacao/login", new { email = ApiFactory.EmailDoAdmin, senha = "senha1234" });

        // Primeira vez: cadastra com caixa diferente da configuração, para provar que a comparação ignora maiúsculas.
        if (!login.IsSuccessStatusCode) return await _api.NovoUsuarioAsync(ApiFactory.EmailDoAdmin.ToUpperInvariant());

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>();
        return new UsuarioDeTeste(cliente, sessao.GetProperty("usuario").GetProperty("id").GetGuid(), sessao);
    }

    [PostgresFact]
    public async Task Sem_login_o_painel_responde_401()
    {
        var resposta = await _api.CreateClient().GetAsync("/admin/resumo");

        Assert.Equal(401, (int)resposta.StatusCode);
    }

    [PostgresFact]
    public async Task Usuario_comum_recebe_403_em_todas_as_rotas_do_painel()
    {
        var comum = await _api.NovoUsuarioAsync();

        foreach (var caminho in new[] { "/admin/resumo", "/admin/series", "/admin/insights", "/admin/usuarios" })
        {
            var (status, _) = await comum.GetAsync(caminho);
            Assert.Equal(403, status);
        }
    }

    [PostgresFact]
    public async Task Sessao_diz_se_a_conta_e_admin_e_o_email_da_configuracao_vale_sem_diferenciar_caixa()
    {
        var comum = await _api.CreateClient().PostAsJsonAsync("/autenticacao/cadastro", new { nome = "Ana", email = $"{Guid.NewGuid():N}@teste.com", senha = "senha1234" });
        var sessaoComum = await comum.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(sessaoComum.GetProperty("ehAdmin").GetBoolean());

        var admin = await AdminAsync();
        var (status, _) = await admin.GetAsync("/admin/resumo");
        Assert.Equal(200, status);

        // A renovação também devolve o papel (o app guarda a sessão renovada).
        var (statusRenovar, renovada) = await admin.EnviarAsync(HttpMethod.Post, "/autenticacao/renovar", new { tokenDeRenovacao = admin.TokenDeRenovacao });
        Assert.Equal(200, statusRenovar);
        Assert.True(renovada.GetProperty("ehAdmin").GetBoolean());
    }

    [PostgresFact]
    public async Task Resumo_conta_usuarios_listas_e_itens()
    {
        var dono = await _api.NovoUsuarioAsync();
        var (_, lista) = await dono.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero", permitirDuplicada = true });
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var (_, item) = await dono.PostAsync($"/listas/{listaId}/itens", new { nome = "Arroz", categoriaId = await dono.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 1 });
        await dono.EnviarAsync(HttpMethod.Put, $"/listas/{listaId}/itens/{item.GetProperty("id").GetGuid()}/comprado", new { comprado = true });

        var admin = await AdminAsync();
        var (status, resumo) = await admin.GetAsync("/admin/resumo");

        Assert.Equal(200, status);
        Assert.True(resumo.GetProperty("usuarios").GetProperty("total").GetInt32() >= 2);
        Assert.True(resumo.GetProperty("usuarios").GetProperty("novos7Dias").GetInt32() >= 2);
        Assert.True(resumo.GetProperty("usuarios").GetProperty("ativos24Horas").GetInt32() >= 2);
        Assert.True(resumo.GetProperty("grupos").GetProperty("total").GetInt32() >= 2);
        Assert.True(resumo.GetProperty("listas").GetProperty("emAndamento").GetInt32() >= 1);
        Assert.True(resumo.GetProperty("itens").GetProperty("comprados").GetInt32() >= 1);
    }

    [PostgresFact]
    public async Task Series_traz_um_ponto_por_dia_e_ajusta_janelas_absurdas()
    {
        await _api.NovoUsuarioAsync();
        var admin = await AdminAsync();

        var (status, series) = await admin.GetAsync("/admin/series?dias=14");
        Assert.Equal(200, status);
        Assert.Equal(14, series.GetProperty("dias").GetInt32());
        Assert.Equal(14, series.GetProperty("cadastros").GetArrayLength());
        Assert.True(series.GetProperty("cadastros").EnumerateArray().Sum(p => p.GetProperty("valor").GetInt32()) >= 1);

        var (_, grande) = await admin.GetAsync("/admin/series?dias=5000");
        Assert.Equal(90, grande.GetProperty("dias").GetInt32());

        var (_, semDias) = await admin.GetAsync("/admin/series");
        Assert.Equal(30, semDias.GetProperty("dias").GetInt32());
    }

    [PostgresFact]
    public async Task Lista_de_usuarios_mostra_conta_grupo_e_atividade()
    {
        var maria = await _api.NovoUsuarioAsync();
        var (_, lista) = await maria.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero", permitirDuplicada = true });
        Assert.NotEqual(default, lista);

        var admin = await AdminAsync();
        var (status, usuarios) = await admin.GetAsync("/admin/usuarios");

        Assert.Equal(200, status);
        var itens = usuarios.GetProperty("itens").EnumerateArray().ToList();
        Assert.NotEmpty(itens);
        var linha = itens.First(i => i.GetProperty("id").GetGuid() == maria.Id);
        Assert.Equal("Maria", linha.GetProperty("nome").GetString());
        Assert.Equal(1, linha.GetProperty("membrosDoGrupo").GetInt32());
        Assert.True(linha.GetProperty("listasCriadas").GetInt32() >= 1);
        Assert.NotEqual(JsonValueKind.Null, linha.GetProperty("ultimoAcessoEm").ValueKind);
    }

    private static async Task<JsonElement> LinhaDaContaAsync(UsuarioDeTeste admin, Guid id)
    {
        var (_, usuarios) = await admin.GetAsync("/admin/usuarios");
        return usuarios.GetProperty("itens").EnumerateArray().First(i => i.GetProperty("id").GetGuid() == id);
    }

    [PostgresFact]
    public async Task Bloquear_derruba_o_token_a_renovacao_e_o_login_e_liberar_devolve_o_acesso()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        var maria = await _api.NovoUsuarioAsync(email);
        var admin = await AdminAsync();
        Assert.Equal(200, (await maria.GetAsync("/categorias")).Status);
        Assert.Equal(JsonValueKind.Null, (await LinhaDaContaAsync(admin, maria.Id)).GetProperty("bloqueadoEm").ValueKind);

        var (statusBloqueio, _) = await admin.PutAsync($"/admin/usuarios/{maria.Id}/bloqueio", new { bloqueado = true });

        Assert.Equal(204, statusBloqueio);
        Assert.NotEqual(JsonValueKind.Null, (await LinhaDaContaAsync(admin, maria.Id)).GetProperty("bloqueadoEm").ValueKind);

        // O token de acesso que ela já tinha deixa de valer na hora, sem esperar os 30 minutos.
        Assert.Equal(401, (await maria.GetAsync("/categorias")).Status);
        Assert.Equal(401, await maria.RenovarAsync());

        var login = await _api.CreateClient().PostAsJsonAsync("/autenticacao/login", new { email, senha = "senha1234" });
        Assert.Equal(401, (int)login.StatusCode);
        Assert.Contains("bloqueada", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());

        // Senha errada não revela que a conta existe nem que está bloqueada.
        var senhaErrada = await _api.CreateClient().PostAsJsonAsync("/autenticacao/login", new { email, senha = "outra-senha-1" });
        Assert.DoesNotContain("bloqueada", (await senhaErrada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());

        var (statusLiberar, _) = await admin.PutAsync($"/admin/usuarios/{maria.Id}/bloqueio", new { bloqueado = false });

        Assert.Equal(204, statusLiberar);
        Assert.Equal(JsonValueKind.Null, (await LinhaDaContaAsync(admin, maria.Id)).GetProperty("bloqueadoEm").ValueKind);
        var voltou = await _api.CreateClient().PostAsJsonAsync("/autenticacao/login", new { email, senha = "senha1234" });
        Assert.Equal(200, (int)voltou.StatusCode);
    }

    [PostgresFact]
    public async Task Administrador_nao_pode_ser_bloqueado_conta_inexistente_da_404_e_usuario_comum_recebe_403()
    {
        var admin = await AdminAsync();
        var comum = await _api.NovoUsuarioAsync();

        var (statusAdmin, _) = await admin.PutAsync($"/admin/usuarios/{admin.Id}/bloqueio", new { bloqueado = true });
        var (statusInexistente, _) = await admin.PutAsync($"/admin/usuarios/{Guid.NewGuid()}/bloqueio", new { bloqueado = true });
        var (statusComum, _) = await comum.PutAsync($"/admin/usuarios/{comum.Id}/bloqueio", new { bloqueado = true });

        Assert.Equal(400, statusAdmin);
        Assert.Equal(404, statusInexistente);
        Assert.Equal(403, statusComum);
        Assert.Equal(200, (await admin.GetAsync("/admin/resumo")).Status);
        Assert.True((await LinhaDaContaAsync(admin, admin.Id)).GetProperty("ehAdmin").GetBoolean());
        Assert.Equal(200, (await comum.GetAsync("/categorias")).Status);
    }

    [PostgresFact]
    public async Task Insights_trazem_funil_gasto_rankings_adocao_e_retencao()
    {
        var u = await _api.NovoUsuarioAsync();
        var (_, lista) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero", permitirDuplicada = true });
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var categoriaId = await u.CategoriaAsync("Mercearia");
        var (_, item) = await u.PostAsync($"/listas/{listaId}/itens", new { nome = "arroz insights", categoriaId, unidade = "Pacote", quantidade = 2 });
        var itemId = item.GetProperty("id").GetGuid();
        await u.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome = "arroz insights", categoriaId, unidade = "Pacote", quantidade = 2, versao = item.GetProperty("versao").GetInt32(), precoUnitario = 25m });
        await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });
        await u.PostAsync($"/listas/{listaId}/finalizar");

        var admin = await AdminAsync();
        var (status, insights) = await admin.GetAsync("/admin/insights");

        Assert.Equal(200, status);

        var retencao = insights.GetProperty("retencao").EnumerateArray().ToList();
        Assert.Equal([1, 7, 30], retencao.Select(r => r.GetProperty("dias").GetInt32()).ToArray());
        Assert.All(retencao, r => Assert.True(r.GetProperty("voltaram").GetInt32() <= r.GetProperty("elegiveis").GetInt32()));

        var funil = insights.GetProperty("funil");
        Assert.True(funil.GetProperty("criaramLista").GetInt32() >= 1);
        Assert.True(funil.GetProperty("compraram").GetInt32() >= 1);
        Assert.True(funil.GetProperty("finalizaram").GetInt32() >= 1);
        Assert.True(funil.GetProperty("criaramLista").GetInt32() <= funil.GetProperty("contas").GetInt32());
        Assert.True(funil.GetProperty("semAtividade").GetInt32() >= 0);

        var gasto = insights.GetProperty("gasto");
        Assert.True(gasto.GetProperty("compras").GetInt32() >= 1);
        Assert.True(gasto.GetProperty("medio").GetDecimal() > 0);
        var novembro = gasto.GetProperty("porMes").EnumerateArray().First(m => m.GetProperty("competencia").GetString() == "2026-11");
        Assert.True(novembro.GetProperty("total").GetDecimal() >= 50m);

        var produtos = insights.GetProperty("produtosMaisComprados").EnumerateArray().Select(p => p.GetProperty("quantidade").GetInt32()).ToList();
        Assert.NotEmpty(produtos);
        Assert.Equal(produtos.OrderByDescending(q => q), produtos);
        Assert.All(insights.GetProperty("produtosMaisComprados").EnumerateArray(), p => Assert.True(char.IsUpper(p.GetProperty("nome").GetString()![0]) || !char.IsLetter(p.GetProperty("nome").GetString()![0])));

        Assert.Equal(["familia", "divisao", "mercado", "nota", "avisos"], insights.GetProperty("adocao").EnumerateArray().Select(a => a.GetProperty("recurso").GetString()!).ToArray());
    }
}
