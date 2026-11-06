using System.Net.Http.Json;
using System.Text.Json;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGroceryApi.Tests.Integracao;

/// <summary>Contato e feedback: o e-mail que o administrador configura, as mensagens de cada pessoa e a caixa de entrada do painel.</summary>
[Collection("api")]
public class ContatoTests
{
    private readonly ApiFactory _api;

    public ContatoTests(ApiFactory api)
    {
        _api = api;
    }

    private async Task<UsuarioDeTeste> AdminAsync()
    {
        var cliente = _api.CreateClient();
        var login = await cliente.PostAsJsonAsync("/autenticacao/login", new { email = ApiFactory.EmailDoAdmin, senha = "senha1234" });
        if (!login.IsSuccessStatusCode) return await _api.NovoUsuarioAsync(ApiFactory.EmailDoAdmin.ToUpperInvariant());

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>();
        return new UsuarioDeTeste(cliente, sessao.GetProperty("usuario").GetProperty("id").GetGuid(), sessao);
    }

    private static object Mensagem(string assunto = "Modo escuro", string texto = "Seria bom ter o modo escuro automático.", string tipo = "Feedback") => new { tipo, assunto, texto };

    [PostgresFact]
    public async Task Sem_login_o_contato_responde_401()
    {
        var cliente = _api.CreateClient();

        Assert.Equal(401, (int)(await cliente.GetAsync("/contato")).StatusCode);
        Assert.Equal(401, (int)(await cliente.GetAsync("/contato/mensagens")).StatusCode);
    }

    [PostgresFact]
    public async Task Informacoes_cadastradas_pelo_admin_aparecem_para_todos_na_ordem_e_sem_descricao_vazia()
    {
        var admin = await AdminAsync();
        var comum = await _api.NovoUsuarioAsync();
        await LimparInformacoesAsync(admin);

        var (status, primeira) = await admin.PostAsync("/admin/contato/informacoes", new { titulo = "  Email para contato ", descricao = "Caso queira mandar um email", valor = " david.alves@teste.com " });
        Assert.Equal(201, status);
        Assert.Equal("Email para contato", primeira.GetProperty("titulo").GetString());
        Assert.Equal("david.alves@teste.com", primeira.GetProperty("valor").GetString());
        await admin.PostAsync("/admin/contato/informacoes", new { titulo = "Chave Pix", descricao = "   ", valor = "abc123" });

        var (_, visto) = await comum.GetAsync("/contato");
        var itens = visto.GetProperty("informacoes").EnumerateArray().ToList();
        Assert.Equal(["Email para contato", "Chave Pix"], itens.Select(i => i.GetProperty("titulo").GetString()));
        Assert.Equal("Caso queira mandar um email", itens[0].GetProperty("descricao").GetString());
        Assert.Equal(JsonValueKind.Null, itens[1].GetProperty("descricao").ValueKind);
    }

    [PostgresFact]
    public async Task Admin_altera_e_remove_informacao()
    {
        var admin = await AdminAsync();
        var comum = await _api.NovoUsuarioAsync();
        await LimparInformacoesAsync(admin);
        var (_, criada) = await admin.PostAsync("/admin/contato/informacoes", new { titulo = "Site", valor = "http://a.com" });
        var id = criada.GetProperty("id").GetGuid();

        var (statusAlterar, alterada) = await admin.PutAsync($"/admin/contato/informacoes/{id}", new { titulo = "Site oficial", descricao = "Novidades", valor = "http://b.com" });
        Assert.Equal(200, statusAlterar);
        Assert.Equal("Novidades", alterada.GetProperty("descricao").GetString());

        var (_, depois) = await comum.GetAsync("/contato");
        Assert.Equal("http://b.com", depois.GetProperty("informacoes")[0].GetProperty("valor").GetString());

        Assert.Equal(204, (await admin.DeleteAsync($"/admin/contato/informacoes/{id}")).Status);
        var (_, vazio) = await comum.GetAsync("/contato");
        Assert.Empty(vazio.GetProperty("informacoes").EnumerateArray());
        Assert.Equal(404, (await admin.DeleteAsync($"/admin/contato/informacoes/{id}")).Status);
        Assert.Equal(404, (await admin.PutAsync($"/admin/contato/informacoes/{id}", new { titulo = "x", valor = "y" })).Status);
    }

    [PostgresFact]
    public async Task Informacao_exige_titulo_e_valor_respeita_limites_e_so_o_admin_mexe()
    {
        var admin = await AdminAsync();
        var comum = await _api.NovoUsuarioAsync();

        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = " ", valor = "x" })).Status);
        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = "x", valor = "" })).Status);
        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = new string('a', 61), valor = "x" })).Status);
        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = "x", descricao = new string('a', 201), valor = "x" })).Status);
        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = "x", valor = new string('a', 301) })).Status);

        Assert.Equal(403, (await comum.PostAsync("/admin/contato/informacoes", new { titulo = "x", valor = "y" })).Status);
        Assert.Equal(403, (await comum.PutAsync($"/admin/contato/informacoes/{Guid.NewGuid()}", new { titulo = "x", valor = "y" })).Status);
        Assert.Equal(403, (await comum.DeleteAsync($"/admin/contato/informacoes/{Guid.NewGuid()}")).Status);
    }

    [PostgresFact]
    public async Task Card_de_contato_aceita_no_maximo_20_informacoes()
    {
        var admin = await AdminAsync();
        await LimparInformacoesAsync(admin);
        for (var i = 0; i < 20; i++) Assert.Equal(201, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = $"Info {i}", valor = "v" })).Status);

        Assert.Equal(400, (await admin.PostAsync("/admin/contato/informacoes", new { titulo = "Demais", valor = "v" })).Status);
        await LimparInformacoesAsync(admin);
    }

    /// <summary>As informações são globais (não há dono): cada teste começa do zero para não depender dos outros.</summary>
    private static async Task LimparInformacoesAsync(UsuarioDeTeste admin)
    {
        var (_, caixa) = await admin.GetAsync("/admin/mensagens");
        foreach (var info in caixa.GetProperty("informacoes").EnumerateArray()) await admin.DeleteAsync($"/admin/contato/informacoes/{info.GetProperty("id").GetGuid()}");
    }

    [PostgresFact]
    public async Task Pessoa_envia_feedback_e_ve_so_as_proprias_mensagens()
    {
        var ana = await _api.NovoUsuarioAsync();
        var bruno = await _api.NovoUsuarioAsync();

        var (status, criada) = await ana.PostAsync("/contato/mensagens", Mensagem());
        await bruno.PostAsync("/contato/mensagens", Mensagem("Outra coisa", "Mensagem do Bruno para o admin."));

        Assert.Equal(201, status);
        Assert.Equal("Recebida", criada.GetProperty("status").GetString());
        Assert.Equal("Feedback", criada.GetProperty("tipo").GetString());
        Assert.False(criada.GetProperty("respostaNova").GetBoolean());

        var (_, minhas) = await ana.GetAsync("/contato/mensagens");
        var itens = minhas.GetProperty("itens").EnumerateArray().ToList();
        Assert.Single(itens);
        Assert.Equal("Modo escuro", itens[0].GetProperty("assunto").GetString());
        Assert.Equal(0, minhas.GetProperty("respostasNovas").GetInt32());
    }

    [PostgresFact]
    public async Task Envio_valida_assunto_texto_e_tipo()
    {
        var ana = await _api.NovoUsuarioAsync();

        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", Mensagem(assunto: "  ab "))).Status);
        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", Mensagem(texto: "oi"))).Status);
        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", Mensagem(assunto: new string('a', 101)))).Status);
        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", Mensagem(texto: new string('a', 2001)))).Status);
        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", Mensagem(tipo: "Invalido"))).Status);
        Assert.Equal(400, (await ana.PostAsync("/contato/mensagens", new { tipo = 99, assunto = "Assunto", texto = "Texto da mensagem" })).Status);
        Assert.Equal(201, (await ana.PostAsync("/contato/mensagens", Mensagem(tipo: "Solicitacao"))).Status);
    }

    [PostgresFact]
    public async Task Limite_diario_de_mensagens_por_pessoa()
    {
        var ana = await _api.NovoUsuarioAsync();
        for (var i = 0; i < 10; i++) Assert.Equal(201, (await ana.PostAsync("/contato/mensagens", Mensagem($"Assunto {i}"))).Status);

        var (status, erro) = await ana.PostAsync("/contato/mensagens", Mensagem("Mais uma"));

        Assert.Equal(400, status);
        Assert.Contains("amanhã", erro.ToString());
    }

    [PostgresFact]
    public async Task Usuario_comum_nao_acessa_a_caixa_do_admin()
    {
        var comum = await _api.NovoUsuarioAsync();

        Assert.Equal(403, (await comum.GetAsync("/admin/mensagens")).Status);
        Assert.Equal(403, (await comum.PutAsync($"/admin/mensagens/{Guid.NewGuid()}", new { resposta = "x", status = "Respondida" })).Status);
    }

    [PostgresFact]
    public async Task Admin_ve_quem_enviou_responde_e_a_pessoa_ve_a_resposta_nova_ate_abrir()
    {
        var admin = await AdminAsync();
        var ana = await _api.NovoUsuarioAsync();
        var (_, criada) = await ana.PostAsync("/contato/mensagens", Mensagem("Rota no Savegnago", "A rota não passa pelo açougue."));
        var id = criada.GetProperty("id").GetGuid();

        var (statusLista, caixa) = await admin.GetAsync("/admin/mensagens");
        Assert.Equal(200, statusLista);
        var daAna = caixa.GetProperty("itens").EnumerateArray().First(m => m.GetProperty("id").GetGuid() == id);
        Assert.Equal(ana.Id, daAna.GetProperty("usuarioId").GetGuid());
        Assert.Equal("Rota no Savegnago", daAna.GetProperty("assunto").GetString());
        Assert.False(string.IsNullOrWhiteSpace(daAna.GetProperty("email").GetString()));
        Assert.True(caixa.GetProperty("aguardandoResposta").GetInt32() >= 1);

        var (statusAnalise, analise) = await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "", status = "EmAnalise" });
        Assert.Equal(200, statusAnalise);
        Assert.Equal("EmAnalise", analise.GetProperty("status").GetString());
        var (_, semResposta) = await ana.GetAsync("/contato/mensagens");
        Assert.Equal(0, semResposta.GetProperty("respostasNovas").GetInt32());

        var (statusResposta, respondida) = await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "  Corrigido na próxima versão.  ", status = "Respondida" });
        Assert.Equal(200, statusResposta);
        Assert.Equal("Corrigido na próxima versão.", respondida.GetProperty("resposta").GetString());

        var (_, comResposta) = await ana.GetAsync("/contato/mensagens");
        Assert.Equal(1, comResposta.GetProperty("respostasNovas").GetInt32());
        var item = comResposta.GetProperty("itens")[0];
        Assert.Equal("Respondida", item.GetProperty("status").GetString());
        Assert.True(item.GetProperty("respostaNova").GetBoolean());

        Assert.Equal(204, (await ana.PutAsync($"/contato/mensagens/{id}/vista")).Status);
        var (_, depois) = await ana.GetAsync("/contato/mensagens");
        Assert.Equal(0, depois.GetProperty("respostasNovas").GetInt32());

        // Reescrever a resposta volta a avisar; só mudar o status, não.
        await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "Corrigido na próxima versão.", status = "Concluida" });
        var (_, semAviso) = await ana.GetAsync("/contato/mensagens");
        Assert.Equal(0, semAviso.GetProperty("respostasNovas").GetInt32());
        Assert.Equal("Concluida", semAviso.GetProperty("itens")[0].GetProperty("status").GetString());

        await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "Publicamos a correção hoje.", status = "Concluida" });
        var (_, novamente) = await ana.GetAsync("/contato/mensagens");
        Assert.Equal(1, novamente.GetProperty("respostasNovas").GetInt32());
    }

    [PostgresFact]
    public async Task Responder_exige_texto_para_marcar_respondida_e_valida_o_pedido()
    {
        var admin = await AdminAsync();
        var ana = await _api.NovoUsuarioAsync();
        var (_, criada) = await ana.PostAsync("/contato/mensagens", Mensagem());
        var id = criada.GetProperty("id").GetGuid();

        Assert.Equal(400, (await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "  ", status = "Respondida" })).Status);
        Assert.Equal(400, (await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = "ok", status = "Inexistente" })).Status);
        Assert.Equal(400, (await admin.PutAsync($"/admin/mensagens/{id}", new { resposta = new string('a', 2001), status = "Respondida" })).Status);
        Assert.Equal(404, (await admin.PutAsync($"/admin/mensagens/{Guid.NewGuid()}", new { resposta = "ok", status = "Respondida" })).Status);
    }

    [PostgresFact]
    public async Task Ninguem_marca_como_vista_a_mensagem_de_outra_pessoa()
    {
        var ana = await _api.NovoUsuarioAsync();
        var bruno = await _api.NovoUsuarioAsync();
        var (_, criada) = await ana.PostAsync("/contato/mensagens", Mensagem());

        var (status, _) = await bruno.PutAsync($"/contato/mensagens/{criada.GetProperty("id").GetGuid()}/vista");

        Assert.Equal(404, status);
    }

    [PostgresFact]
    public async Task Encerrar_a_conta_apaga_as_mensagens_da_pessoa()
    {
        var ana = await _api.NovoUsuarioAsync();
        await ana.PostAsync("/contato/mensagens", Mensagem());
        Assert.Equal(1, await ContarNoBancoAsync(ana.Id));

        var (status, _) = await ana.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = await EmailDaContaAsync(ana.Id) });

        Assert.Equal(204, status);
        Assert.Equal(0, await ContarNoBancoAsync(ana.Id));
    }

    private async Task<int> ContarNoBancoAsync(Guid usuarioId)
    {
        await using var escopo = _api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<HomeGroceryDbContext>().MensagensDeContato.CountAsync(m => m.UsuarioId == usuarioId);
    }

    private async Task<string> EmailDaContaAsync(Guid usuarioId)
    {
        await using var escopo = _api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<HomeGroceryDbContext>().Users.Where(u => u.Id == usuarioId).Select(u => u.Email!).SingleAsync();
    }
}
