using System.Net.Http.Json;
using System.Text.Json;
using HomeGroceryApi.Tests.Fakes;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class LoginComGoogleTests
{
    private readonly ApiFactory _api;

    public LoginComGoogleTests(ApiFactory api)
    {
        _api = api;
    }

    private async Task<(int Status, JsonElement Corpo)> EntrarAsync(string idToken)
    {
        using var cliente = _api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/autenticacao/login-google", new { idToken });
        var texto = await resposta.Content.ReadAsStringAsync();
        return ((int)resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    [PostgresFact]
    public async Task Primeiro_acesso_cria_a_conta_com_o_nome_do_google_e_a_sessao_funciona()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";

        var (status, sessao) = await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token(email, "Ana Souza"));

        Assert.Equal(200, status);
        Assert.Equal("Ana Souza", sessao.GetProperty("usuario").GetProperty("nome").GetString());
        Assert.Equal(email, sessao.GetProperty("usuario").GetProperty("email").GetString());

        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", sessao.GetProperty("tokenDeAcesso").GetString());
        var categorias = await cliente.GetAsync("/categorias");
        Assert.Equal(200, (int)categorias.StatusCode);
        Assert.NotEmpty((await categorias.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [PostgresFact]
    public async Task Segundo_acesso_entra_na_mesma_conta_e_conta_com_senha_existente_tambem_entra()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        var (_, primeira) = await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token(email, "Ana"));
        var (_, segunda) = await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token(email, "Outro Nome"));
        Assert.Equal(primeira.GetProperty("usuario").GetProperty("id").GetString(), segunda.GetProperty("usuario").GetProperty("id").GetString());
        Assert.Equal(primeira.GetProperty("grupoId").GetString(), segunda.GetProperty("grupoId").GetString());

        var comSenha = $"{Guid.NewGuid():N}@teste.com";
        var usuario = await _api.NovoUsuarioAsync(comSenha);
        var (status, entrou) = await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token(comSenha));
        Assert.Equal(200, status);
        Assert.Equal(comSenha, entrou.GetProperty("usuario").GetProperty("email").GetString());
        Assert.NotNull(usuario);
    }

    [PostgresFact]
    public async Task Token_invalido_e_recusado_e_conta_do_google_nao_aceita_senha_adivinhada()
    {
        Assert.Equal(401, (await EntrarAsync("lixo")).Status);
        Assert.Equal(401, (await EntrarAsync(string.Empty)).Status);

        var email = $"{Guid.NewGuid():N}@teste.com";
        await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token(email, "Ana"));
        using var cliente = _api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/autenticacao/login", new { email, senha = "senha1234" });
        Assert.Equal(401, (int)resposta.StatusCode);
    }

    [PostgresFact]
    public async Task Sem_nome_no_google_usa_o_inicio_do_email_e_o_app_so_mostra_o_botao_com_client_id()
    {
        var (_, sessao) = await EntrarAsync(ValidadorDeLoginGoogleDeTeste.Token("joao.silva@teste.com"));
        Assert.Equal("joao.silva", sessao.GetProperty("usuario").GetProperty("nome").GetString());

        using var cliente = _api.CreateClient();
        var ligado = await cliente.GetFromJsonAsync<JsonElement>("/autenticacao/google");
        Assert.Equal("cliente-de-teste.apps.googleusercontent.com", ligado.GetProperty("clienteId").GetString());

        _api.Google.ClienteId = null;
        try
        {
            var desligado = await cliente.GetFromJsonAsync<JsonElement>("/autenticacao/google");
            Assert.Equal(JsonValueKind.Null, desligado.GetProperty("clienteId").ValueKind);
        }
        finally
        {
            _api.Google.ClienteId = "cliente-de-teste.apps.googleusercontent.com";
        }
    }
}
