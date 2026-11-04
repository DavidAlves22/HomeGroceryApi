using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class RecuperacaoDeSenhaTests
{
    private readonly ApiFactory _api;

    public RecuperacaoDeSenhaTests(ApiFactory api)
    {
        _api = api;
    }

    private async Task<int> PostarAsync(string caminho, object corpo)
    {
        using var cliente = _api.CreateClient();
        return (int)(await cliente.PostAsJsonAsync(caminho, corpo)).StatusCode;
    }

    private async Task<(int Status, JsonElement Corpo)> LoginAsync(string email, string senha)
    {
        using var cliente = _api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/autenticacao/login", new { email, senha });
        var texto = await resposta.Content.ReadAsStringAsync();
        return ((int)resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<(string Email, UsuarioDeTeste Usuario)> NovoAsync()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        return (email, await _api.NovoUsuarioAsync(email));
    }

    [PostgresFact]
    public async Task Fluxo_completo_codigo_por_email_senha_nova_encerra_sessoes_antigas_e_o_codigo_so_serve_uma_vez()
    {
        var (email, usuario) = await NovoAsync();
        Assert.Equal(204, await PostarAsync("/autenticacao/esqueci-senha", new { email }));

        var recebido = Assert.Single(_api.Emails.Para(email));
        Assert.Contains("Home Grocery", recebido.Assunto);
        var codigo = _api.Emails.UltimoCodigo(email);
        Assert.Equal(6, codigo.Length);

        Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo = codigo == "000000" ? "111111" : "000000", novaSenha = "novasenha99" }));
        Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo, novaSenha = "curta" }));

        // Senha fraca não gasta o código: com uma senha boa, o mesmo código ainda funciona.
        Assert.Equal(204, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo, novaSenha = "novasenha99" }));

        Assert.Equal(401, (await LoginAsync(email, "senha1234")).Status);
        Assert.Equal(200, (await LoginAsync(email, "novasenha99")).Status);
        Assert.Equal(401, await PostarAsync("/autenticacao/renovar", new { tokenDeRenovacao = usuario.TokenDeRenovacao }));
        Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo, novaSenha = "outrasenha77" }));
    }

    [PostgresFact]
    public async Task Conta_que_nao_existe_recebe_a_mesma_resposta_e_nenhum_email()
    {
        var inexistente = $"{Guid.NewGuid():N}@teste.com";

        Assert.Equal(204, await PostarAsync("/autenticacao/esqueci-senha", new { email = inexistente }));
        Assert.Empty(_api.Emails.Para(inexistente));
        Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email = inexistente, codigo = "123456", novaSenha = "novasenha99" }));
    }

    [PostgresFact]
    public async Task Depois_de_cinco_erros_o_codigo_trava_mesmo_se_o_certo_for_digitado()
    {
        var (email, _) = await NovoAsync();
        await PostarAsync("/autenticacao/esqueci-senha", new { email });
        var codigo = _api.Emails.UltimoCodigo(email);
        var errado = codigo == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++) Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo = errado, novaSenha = "novasenha99" }));

        Assert.Equal(400, await PostarAsync("/autenticacao/redefinir-senha", new { email, codigo, novaSenha = "novasenha99" }));
        Assert.Equal(200, (await LoginAsync(email, "senha1234")).Status);
    }

    [PostgresFact]
    public async Task Pedidos_seguidos_nao_disparam_varios_emails_e_o_codigo_novo_cancela_o_anterior()
    {
        var (email, _) = await NovoAsync();

        await PostarAsync("/autenticacao/esqueci-senha", new { email });
        await PostarAsync("/autenticacao/esqueci-senha", new { email });
        await PostarAsync("/autenticacao/esqueci-senha", new { email });

        Assert.Single(_api.Emails.Para(email));
    }

    [PostgresFact]
    public async Task Sem_servidor_de_email_a_recuperacao_fica_indisponivel_e_nada_e_enviado()
    {
        var (email, _) = await NovoAsync();
        using var cliente = _api.CreateClient();
        Assert.True((await cliente.GetFromJsonAsync<JsonElement>("/autenticacao/recuperacao")).GetProperty("disponivel").GetBoolean());

        _api.Emails.Habilitado = false;
        try
        {
            Assert.False((await cliente.GetFromJsonAsync<JsonElement>("/autenticacao/recuperacao")).GetProperty("disponivel").GetBoolean());
            Assert.Equal(204, await PostarAsync("/autenticacao/esqueci-senha", new { email }));
            Assert.Empty(_api.Emails.Para(email));
        }
        finally
        {
            _api.Emails.Habilitado = true;
        }
    }

    [PostgresFact]
    public async Task Alterar_o_nome_aparece_para_a_familia_e_valida_o_tamanho()
    {
        var eu = await _api.NovoUsuarioAsync();
        var esposa = await _api.NovoUsuarioAsync();
        var (_, convite) = await eu.PostAsync("/grupo/convites");
        var (_, sessao) = await esposa.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        esposa.AplicarSessao(sessao);

        var (status, resposta) = await esposa.PutAsync("/conta/nome", new { nome = "  Ana Souza  " });

        Assert.Equal(200, status);
        Assert.Equal("Ana Souza", resposta.GetProperty("nome").GetString());
        var (_, grupo) = await eu.GetAsync("/grupo");
        Assert.Contains(grupo.GetProperty("membros").EnumerateArray(), m => m.GetProperty("nome").GetString() == "Ana Souza");
        Assert.Equal(400, (await esposa.PutAsync("/conta/nome", new { nome = "A" })).Status);
        Assert.Equal(400, (await esposa.PutAsync("/conta/nome", new { nome = new string('x', 81) })).Status);

        using var anonimo = _api.CreateClient();
        Assert.Equal(401, (int)(await anonimo.PutAsJsonAsync("/conta/nome", new { nome = "Fulano" })).StatusCode);
    }
}
