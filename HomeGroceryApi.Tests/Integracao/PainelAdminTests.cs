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

        foreach (var caminho in new[] { "/admin/resumo", "/admin/series", "/admin/usuarios" })
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
}
