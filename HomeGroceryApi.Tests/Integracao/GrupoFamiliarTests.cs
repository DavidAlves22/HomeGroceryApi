using HomeGroceryApi.Domain.Servicos;
using System.Threading.Channels;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class GrupoFamiliarTests
{
    private readonly ApiFactory _api;

    public GrupoFamiliarTests(ApiFactory api)
    {
        _api = api;
    }

    /// <summary>Cria dois usuários e coloca o segundo na família do primeiro (convite + entrada).</summary>
    private async Task<(UsuarioDeTeste Admin, UsuarioDeTeste Membro)> FamiliaAsync()
    {
        var admin = await _api.NovoUsuarioAsync();
        var membro = await _api.NovoUsuarioAsync();
        await EntrarAsync(admin, membro);
        return (admin, membro);
    }

    private static async Task EntrarAsync(UsuarioDeTeste admin, UsuarioDeTeste quemEntra)
    {
        var (statusConvite, convite) = await admin.PostAsync("/grupo/convites");
        Assert.Equal(201, statusConvite);
        var (statusEntrar, sessao) = await quemEntra.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        Assert.Equal(200, statusEntrar);
        quemEntra.AplicarSessao(sessao);
    }

    private static async Task<(Guid ListaId, Guid ItemId)> ListaComItemAsync(UsuarioDeTeste u, string nome = "Arroz")
    {
        var (_, lista) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero", permitirDuplicada = true });
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var (_, item) = await u.PostAsync($"/listas/{listaId}/itens", new { nome, categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 2 });
        return (listaId, item.GetProperty("id").GetGuid());
    }

    [PostgresFact]
    public async Task Convite_tem_codigo_de_8_caracteres_e_quem_entra_passa_a_ver_os_dados_da_familia()
    {
        var admin = await _api.NovoUsuarioAsync();
        var (listaId, _) = await ListaComItemAsync(admin);
        var membro = await _api.NovoUsuarioAsync();
        var gruposAntes = membro.GrupoId;

        var (_, convite) = await admin.PostAsync("/grupo/convites");
        var codigo = convite.GetProperty("codigo").GetString()!;
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", codigo);

        var (status, sessao) = await membro.PostAsync("/grupo/entrar", new { codigo = codigo.ToLowerInvariant().Replace("-", " ") });
        Assert.Equal(200, status);
        membro.AplicarSessao(sessao);

        Assert.Equal(admin.GrupoId, membro.GrupoId);
        Assert.NotEqual(gruposAntes, membro.GrupoId);
        var (_, listas) = await membro.GetAsync("/listas");
        Assert.Equal(listaId, listas[0].GetProperty("id").GetGuid());
        var (_, categorias) = await membro.GetAsync("/categorias");
        Assert.Equal(CategoriasIniciais.Nomes.Count, categorias.GetArrayLength());
    }

    [PostgresFact]
    public async Task Grupo_lista_membros_com_papeis_e_marca_quem_e_voce()
    {
        var (admin, membro) = await FamiliaAsync();

        var (_, grupo) = await admin.GetAsync("/grupo");

        Assert.True(grupo.GetProperty("compartilhado").GetBoolean());
        Assert.Equal("Administrador", grupo.GetProperty("meuPapel").GetString());
        var membros = grupo.GetProperty("membros").EnumerateArray().ToList();
        Assert.Equal(2, membros.Count);
        Assert.Equal("Administrador", membros[0].GetProperty("papel").GetString());
        Assert.True(membros[0].GetProperty("voce").GetBoolean());
        Assert.Equal("Membro", membros[1].GetProperty("papel").GetString());
        Assert.Equal(membro.Id, membros[1].GetProperty("usuarioId").GetGuid());
        var (_, visaoDoMembro) = await membro.GetAsync("/grupo");
        Assert.Equal("Membro", visaoDoMembro.GetProperty("meuPapel").GetString());
    }

    [PostgresFact]
    public async Task Convite_e_de_uso_unico_e_codigos_invalidos_dao_a_mesma_resposta()
    {
        var admin = await _api.NovoUsuarioAsync();
        var primeiro = await _api.NovoUsuarioAsync();
        var segundo = await _api.NovoUsuarioAsync();
        var (_, convite) = await admin.PostAsync("/grupo/convites");
        var codigo = convite.GetProperty("codigo").GetString();
        Assert.Equal(200, (await primeiro.PostAsync("/grupo/entrar", new { codigo })).Status);

        var (reuso, erroReuso) = await segundo.PostAsync("/grupo/entrar", new { codigo });
        var (inexistente, erroInexistente) = await segundo.PostAsync("/grupo/entrar", new { codigo = "ZZZZ-ZZZZ" });

        Assert.Equal(404, reuso);
        Assert.Equal(404, inexistente);
        Assert.Equal(erroReuso.GetProperty("detail").GetString(), erroInexistente.GetProperty("detail").GetString());
        Assert.Equal(400, (await segundo.PostAsync("/grupo/entrar", new { codigo = "123" })).Status);
    }

    [PostgresFact]
    public async Task So_administrador_convida_renomeia_e_remove_membros()
    {
        var (admin, membro) = await FamiliaAsync();

        Assert.Equal(403, (await membro.PostAsync("/grupo/convites")).Status);
        Assert.Equal(403, (await membro.PutAsync("/grupo", new { nome = "Minha casa" })).Status);
        Assert.Equal(403, (await membro.DeleteAsync($"/grupo/membros/{admin.Id}")).Status);

        Assert.Equal(204, (await admin.PutAsync("/grupo", new { nome = "Família Silva" })).Status);
        var (_, grupo) = await membro.GetAsync("/grupo");
        Assert.Equal("Família Silva", grupo.GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Quem_ja_esta_em_uma_familia_nao_entra_em_outra_nem_na_mesma_duas_vezes()
    {
        var (admin, membro) = await FamiliaAsync();
        var outroAdmin = await _api.NovoUsuarioAsync();
        var (_, conviteOutro) = await outroAdmin.PostAsync("/grupo/convites");
        var (_, conviteDaMesma) = await admin.PostAsync("/grupo/convites");

        var (entrarEmOutra, erro) = await membro.PostAsync("/grupo/entrar", new { codigo = conviteOutro.GetProperty("codigo").GetString() });
        var (entrarNaMesma, _) = await membro.PostAsync("/grupo/entrar", new { codigo = conviteDaMesma.GetProperty("codigo").GetString() });

        Assert.Equal(409, entrarEmOutra);
        Assert.Contains("Saia do grupo atual", erro.GetProperty("detail").GetString());
        Assert.Equal(409, entrarNaMesma);
    }

    [PostgresFact]
    public async Task Item_marcado_por_um_membro_aparece_para_o_outro_com_o_nome_de_quem_marcou()
    {
        var (admin, membro) = await FamiliaAsync();
        var (listaId, itemId) = await ListaComItemAsync(admin);

        var (status, _) = await membro.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });

        Assert.Equal(200, status);
        var (_, naVisaoDoAdmin) = await admin.GetAsync($"/listas/{listaId}");
        var item = naVisaoDoAdmin.GetProperty("itens")[0];
        Assert.True(item.GetProperty("comprado").GetBoolean());
        Assert.Equal(membro.Id, item.GetProperty("compradoPorUsuarioId").GetGuid());
        Assert.Equal(admin.Id, item.GetProperty("adicionadoPorUsuarioId").GetGuid());
    }

    [PostgresFact]
    public async Task Dois_membros_editando_o_mesmo_item_com_a_mesma_versao_geram_conflito()
    {
        var (admin, membro) = await FamiliaAsync();
        var (listaId, itemId) = await ListaComItemAsync(admin);
        var (_, lista) = await admin.GetAsync($"/listas/{listaId}");
        var versao = lista.GetProperty("itens")[0].GetProperty("versao").GetInt32();
        var categoria = await admin.CategoriaAsync("Mercearia");

        var (primeiro, _) = await admin.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 3, versao });
        var (segundo, erro) = await membro.PutAsync($"/listas/{listaId}/itens/{itemId}", new { nome = "Arroz", categoriaId = categoria, unidade = "Pacote", quantidade = 9, versao });

        Assert.Equal(200, primeiro);
        Assert.Equal(409, segundo);
        Assert.Contains("outra pessoa", erro.GetProperty("detail").GetString());
        var (_, final) = await admin.GetAsync($"/listas/{listaId}");
        Assert.Equal(3, final.GetProperty("itens")[0].GetProperty("quantidade").GetDecimal());
    }

    [PostgresFact]
    public async Task Marcacoes_simultaneas_de_itens_diferentes_nao_conflitam()
    {
        var (admin, membro) = await FamiliaAsync();
        var (listaId, arroz) = await ListaComItemAsync(admin);
        var (_, leite) = await admin.PostAsync($"/listas/{listaId}/itens", new { nome = "Leite", categoriaId = await admin.CategoriaAsync("Laticínios"), unidade = "Litro", quantidade = 6 });

        var resultados = await Task.WhenAll(
            admin.PutAsync($"/listas/{listaId}/itens/{arroz}/comprado", new { comprado = true }),
            membro.PutAsync($"/listas/{listaId}/itens/{leite.GetProperty("id").GetGuid()}/comprado", new { comprado = true }));

        Assert.All(resultados, r => Assert.Equal(200, r.Status));
        var (_, final) = await admin.GetAsync($"/listas/{listaId}");
        Assert.Equal(2, final.GetProperty("resumo").GetProperty("comprados").GetInt32());
    }

    [PostgresFact]
    public async Task Membro_removido_perde_o_acesso_na_hora_e_volta_ao_proprio_grupo()
    {
        var membro = await _api.NovoUsuarioAsync();
        var (listaPessoal, _) = await ListaComItemAsync(membro, "Item pessoal");
        var admin = await _api.NovoUsuarioAsync();
        await ListaComItemAsync(admin, "Item da familia");
        var grupoPessoal = membro.GrupoId;
        await EntrarAsync(admin, membro);
        Assert.Equal(200, (await membro.GetAsync("/listas")).Status);

        Assert.Equal(204, (await admin.DeleteAsync($"/grupo/membros/{membro.Id}")).Status);

        // O token antigo ainda está dentro da validade, mas o grupo dele já não vale.
        Assert.Equal(401, (await membro.GetAsync("/listas")).Status);
        Assert.Equal(401, (await membro.GetAsync("/categorias")).Status);
        Assert.Equal(200, await membro.RenovarAsync());
        Assert.Equal(grupoPessoal, membro.GrupoId);
        var (_, listas) = await membro.GetAsync("/listas");
        Assert.Equal(listaPessoal, listas[0].GetProperty("id").GetGuid());
        Assert.Equal(1, listas.GetArrayLength());
    }

    [PostgresFact]
    public async Task Sair_do_grupo_devolve_a_sessao_do_grupo_pessoal()
    {
        var (admin, membro) = await FamiliaAsync();
        var grupoDaFamilia = admin.GrupoId;

        var (status, sessao) = await membro.PostAsync("/grupo/sair");

        Assert.Equal(200, status);
        membro.AplicarSessao(sessao);
        Assert.NotEqual(grupoDaFamilia, membro.GrupoId);
        var (_, grupo) = await admin.GetAsync("/grupo");
        Assert.Equal(1, grupo.GetProperty("membros").GetArrayLength());
        Assert.Equal(409, (await membro.PostAsync("/grupo/sair")).Status);
    }

    [PostgresFact]
    public async Task Administrador_que_sai_promove_outro_membro_e_ganha_um_grupo_novo()
    {
        var (admin, membro) = await FamiliaAsync();
        await ListaComItemAsync(admin);
        var grupoDaFamilia = admin.GrupoId;

        var (status, sessao) = await admin.PostAsync("/grupo/sair");

        Assert.Equal(200, status);
        admin.AplicarSessao(sessao);
        Assert.NotEqual(grupoDaFamilia, admin.GrupoId);
        var (_, listasDoAdmin) = await admin.GetAsync("/listas");
        Assert.Equal(0, listasDoAdmin.GetArrayLength());
        Assert.Equal(CategoriasIniciais.Nomes.Count, (await admin.GetAsync("/categorias")).Corpo.GetArrayLength());
        var (_, grupo) = await membro.GetAsync("/grupo");
        Assert.Equal("Administrador", grupo.GetProperty("meuPapel").GetString());
        Assert.Equal(1, (await membro.GetAsync("/listas")).Corpo.GetArrayLength());
    }

    [PostgresFact]
    public async Task Quem_nao_e_da_familia_nao_ve_nada_dela()
    {
        var (admin, _) = await FamiliaAsync();
        var estranho = await _api.NovoUsuarioAsync();
        var (listaId, _) = await ListaComItemAsync(admin);

        Assert.Equal(404, (await estranho.GetAsync($"/listas/{listaId}")).Status);
        Assert.Equal(0, (await estranho.GetAsync("/listas")).Corpo.GetArrayLength());
        Assert.Equal(1, (await estranho.GetAsync("/grupo")).Corpo.GetProperty("membros").GetArrayLength());
    }

    [PostgresFact]
    public async Task Outro_membro_recebe_em_tempo_real_o_aviso_de_que_o_item_foi_marcado()
    {
        var (admin, membro) = await FamiliaAsync();
        var (listaId, itemId) = await ListaComItemAsync(admin);
        await using var conexao = await ConectarAsync(membro);
        var eventos = conexao.Eventos;

        await admin.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });

        var evento = await AguardarAsync(eventos, e => e.GetProperty("tipo").GetString() == "lista");
        Assert.Equal(listaId, evento.GetProperty("listaId").GetGuid());
        Assert.Equal(itemId, evento.GetProperty("itemId").GetGuid());
        Assert.Equal(admin.Id, evento.GetProperty("porUsuarioId").GetGuid());
    }

    [PostgresFact]
    public async Task Eventos_so_chegam_para_o_proprio_grupo()
    {
        var (admin, membro) = await FamiliaAsync();
        var estranho = await _api.NovoUsuarioAsync();
        var (listaId, itemId) = await ListaComItemAsync(admin);
        await using var doEstranho = await ConectarAsync(estranho);
        var eventosDoEstranho = doEstranho.Eventos;
        await using var daFamilia = await ConectarAsync(membro);
        var eventosDaFamilia = daFamilia.Eventos;

        await admin.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true });

        await AguardarAsync(eventosDaFamilia, e => e.GetProperty("tipo").GetString() == "lista");
        await Task.Delay(500);
        Assert.False(eventosDoEstranho.Reader.TryRead(out _));
    }

    [PostgresFact]
    public async Task Avisa_o_grupo_quando_alguem_entra_e_avisa_quem_foi_removido()
    {
        var admin = await _api.NovoUsuarioAsync();
        var membro = await _api.NovoUsuarioAsync();
        await using var doAdmin = await ConectarAsync(admin);
        var eventosDoAdmin = doAdmin.Eventos;
        await using var doMembro = await ConectarAsync(membro);
        var eventosDoMembro = doMembro.Eventos;

        await EntrarAsync(admin, membro);

        await AguardarAsync(eventosDoAdmin, e => e.GetProperty("tipo").GetString() == "membros");
        await AguardarAsync(eventosDoMembro, e => e.GetProperty("tipo").GetString() == "grupoAlterado");

        await admin.DeleteAsync($"/grupo/membros/{membro.Id}");
        await AguardarAsync(eventosDoMembro, e => e.GetProperty("tipo").GetString() == "grupoAlterado");
    }

    [PostgresFact]
    public async Task Conexao_em_tempo_real_exige_token_e_membro_ativo()
    {
        var (admin, membro) = await FamiliaAsync();
        await admin.DeleteAsync($"/grupo/membros/{membro.Id}");

        var semToken = new HubConnectionBuilder().WithUrl(new Uri(_api.Server.BaseAddress, "/hub/grupo"), o => { o.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler(); o.Transports = HttpTransportType.LongPolling; }).Build();
        await Assert.ThrowsAnyAsync<Exception>(() => semToken.StartAsync());

        // Removido: o token ainda é válido, mas o grupo dele não é mais.
        await using var removido = Criar(membro);
        await Assert.ThrowsAnyAsync<Exception>(() => removido.StartAsync());
    }

    private HubConnection Criar(UsuarioDeTeste usuario)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_api.Server.BaseAddress, "/hub/grupo"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(usuario.TokenDeAcesso);
            })
            .Build();
    }

    /// <summary>Conexão em tempo real aberta + a fila dos eventos que chegam por ela.</summary>
    private sealed class Escuta : IAsyncDisposable
    {
        public Escuta(HubConnection conexao, Channel<JsonElement> eventos)
        {
            Conexao = conexao;
            Eventos = eventos;
        }

        public HubConnection Conexao { get; }
        public Channel<JsonElement> Eventos { get; }

        public ValueTask DisposeAsync() => Conexao.DisposeAsync();
    }

    private async Task<Escuta> ConectarAsync(UsuarioDeTeste usuario)
    {
        var canal = Channel.CreateUnbounded<JsonElement>();
        var conexao = Criar(usuario);
        conexao.On<JsonElement>("evento", e => canal.Writer.TryWrite(e));
        await conexao.StartAsync();
        return new Escuta(conexao, canal);
    }

    private static async Task<JsonElement> AguardarAsync(Channel<JsonElement> eventos, Func<JsonElement, bool> filtro)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (true)
        {
            var evento = await eventos.Reader.ReadAsync(limite.Token);
            if (filtro(evento)) return evento;
        }
    }
}
