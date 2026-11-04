using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace HomeGroceryApi.Tests.Integracao;

/// <summary>
/// Sobe a API inteira em memória apontando para um banco PostgreSQL descartável (um por execução).
/// Precisa do Postgres do docker-compose em localhost:5433; sem ele, os testes são ignorados.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Servidor = "Host=localhost;Port=5433;Username=homegrocery;Password=dev-local-apenas";
    private readonly string _banco = $"homegrocery_teste_{Guid.NewGuid():N}";

    public static bool PostgresDisponivel { get; } = TestarPostgres();

    public ApiFactory()
    {
        if (!PostgresDisponivel) return;

        // Variáveis de ambiente valem antes de a API ler a configuração (appsettings fica por baixo delas).
        Environment.SetEnvironmentVariable("ConnectionStrings__Padrao", $"{Servidor};Database={_banco}");
        Environment.SetEnvironmentVariable("Seguranca__TentativasDeAutenticacaoPorMinuto", "100000");
    }

    /// <summary>Os avisos por push que a API tentou enviar (o envio de verdade é trocado por este registro).</summary>
    public NotificadorDePushDeTeste Avisos { get; } = new();

    /// <summary>Os e-mails de recuperação de senha que a API tentou enviar (o SMTP é trocado por este registro).</summary>
    public EnviadorDeEmailDeTeste Emails { get; } = new();

    /// <summary>Trocado no lugar da validação real do Google (token de teste: "ok|email|nome").</summary>
    public ValidadorDeLoginGoogleDeTeste Google { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IValidadorDeLoginGoogle>();
            servicos.AddSingleton<IValidadorDeLoginGoogle>(Google);
            servicos.RemoveAll<INotificadorDePush>();
            servicos.AddSingleton<INotificadorDePush>(Avisos);
            servicos.RemoveAll<IEnviadorDeEmail>();
            servicos.AddSingleton<IEnviadorDeEmail>(Emails);
        });
    }

    public async Task<UsuarioDeTeste> NovoUsuarioAsync(string? email = null)
    {
        var cliente = CreateClient();
        email ??= $"{Guid.NewGuid():N}@teste.com";
        var resposta = await cliente.PostAsJsonAsync("/autenticacao/cadastro", new { nome = "Maria", email, senha = "senha1234" });
        resposta.EnsureSuccessStatusCode();

        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return new UsuarioDeTeste(cliente, sessao.GetProperty("usuario").GetProperty("id").GetGuid(), sessao);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (!PostgresDisponivel) return;

        await using var conexao = new NpgsqlConnection($"{Servidor};Database=postgres");
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_banco}\" WITH (FORCE)", conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private static bool TestarPostgres()
    {
        try
        {
            using var conexao = new NpgsqlConnection($"{Servidor};Database=postgres;Timeout=3");
            conexao.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Cliente HTTP já autenticado como um usuário recém-cadastrado (cada um tem o próprio grupo).</summary>
public sealed class UsuarioDeTeste
{
    public UsuarioDeTeste(HttpClient http, Guid id, JsonElement sessao)
    {
        Http = http;
        Id = id;
        AplicarSessao(sessao);
    }

    public HttpClient Http { get; }
    public Guid Id { get; }
    public string TokenDeAcesso { get; private set; } = string.Empty;
    public string TokenDeRenovacao { get; private set; } = string.Empty;
    public Guid GrupoId { get; private set; }

    /// <summary>Passa a usar a sessão devolvida pela API (ex.: depois de entrar ou sair de um grupo).</summary>
    public void AplicarSessao(JsonElement sessao)
    {
        TokenDeAcesso = sessao.GetProperty("tokenDeAcesso").GetString()!;
        TokenDeRenovacao = sessao.GetProperty("tokenDeRenovacao").GetString()!;
        GrupoId = sessao.GetProperty("grupoId").GetGuid();
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeAcesso);
    }

    /// <summary>Renova a sessão como o app faz depois de um 401.</summary>
    public async Task<int> RenovarAsync()
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/autenticacao/renovar", new { tokenDeRenovacao = TokenDeRenovacao });
        if (status == 200) AplicarSessao(corpo);
        return status;
    }

    public async Task<(int Status, JsonElement Corpo)> EnviarAsync(HttpMethod metodo, string caminho, object? corpo = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, caminho);
        if (corpo is not null) requisicao.Content = JsonContent.Create(corpo);

        using var resposta = await Http.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return ((int)resposta.StatusCode, json);
    }

    public Task<(int Status, JsonElement Corpo)> GetAsync(string caminho) => EnviarAsync(HttpMethod.Get, caminho);

    public Task<(int Status, JsonElement Corpo)> PostAsync(string caminho, object? corpo = null) => EnviarAsync(HttpMethod.Post, caminho, corpo);

    public Task<(int Status, JsonElement Corpo)> PutAsync(string caminho, object? corpo = null) => EnviarAsync(HttpMethod.Put, caminho, corpo);

    public Task<(int Status, JsonElement Corpo)> DeleteAsync(string caminho) => EnviarAsync(HttpMethod.Delete, caminho);

    /// <summary>Id da categoria padrão com esse nome (ex.: "Mercearia").</summary>
    public async Task<Guid> CategoriaAsync(string nome)
    {
        var (_, categorias) = await GetAsync("/categorias");
        return categorias.EnumerateArray().First(c => c.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
}

/// <summary>Teste de integração: vira "ignorado" (e não "falhou") quando o Postgres local não está no ar.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!ApiFactory.PostgresDisponivel) Skip = "PostgreSQL local indisponível (rode: docker compose up -d banco).";
    }
}
