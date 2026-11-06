using System.Net.Http.Json;
using System.Text.Json;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Infrastructure.Persistencia;
using HomeGroceryApi.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGroceryApi.Tests.Integracao;

/// <summary>Encerrar conta, saída de família, exclusão de conta e de mapa pelo administrador: o que some do banco e o que fica.</summary>
[Collection("api")]
public class ExclusaoDeDadosTests
{
    private readonly ApiFactory _api;

    public ExclusaoDeDadosTests(ApiFactory api)
    {
        _api = api;
    }

    private async Task<T> NoBancoAsync<T>(Func<HomeGroceryDbContext, Task<T>> consulta)
    {
        await using var escopo = _api.Services.CreateAsyncScope();
        return await consulta(escopo.ServiceProvider.GetRequiredService<HomeGroceryDbContext>());
    }

    private async Task<UsuarioDeTeste> AdminAsync()
    {
        var cliente = _api.CreateClient();
        var login = await cliente.PostAsJsonAsync("/autenticacao/login", new { email = ApiFactory.EmailDoAdmin, senha = "senha1234" });
        if (!login.IsSuccessStatusCode) return await _api.NovoUsuarioAsync(ApiFactory.EmailDoAdmin.ToUpperInvariant());

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>();
        return new UsuarioDeTeste(cliente, sessao.GetProperty("usuario").GetProperty("id").GetGuid(), sessao);
    }

    private static object Area(int x, int y, int largura, int altura) => new { x, y, largura, altura };

    /// <summary>Mercado com o número pedido de seções (todas com nome próprio, para cumprir a regra do catálogo).</summary>
    private static async Task<Guid> MercadoAsync(UsuarioDeTeste u, string nome, int secoes = 8)
    {
        var (status, loja) = await u.PostAsync("/lojas", new { nome, cidade = "Araras" });
        Assert.Equal(201, status);
        var mercearia = await u.CategoriaAsync("Mercearia");
        var lista = Enumerable.Range(0, secoes)
            .Select(i => (object)new { nome = $"Corredor {i + 1}", nomeLadoB = (string?)null, dividida = false, area = Area(1 + 3 * i, 8, 2, 6), categoriasLadoA = new[] { mercearia }, categoriasLadoB = Array.Empty<Guid>() })
            .ToArray();
        var corpo = new { nome, largura = 40, altura = 20, entrada = new { x = 2, y = 18 }, caixas = Area(30, 17, 5, 2), secoes = lista, versao = loja.GetProperty("versao").GetInt32() };
        var (statusMapa, salvo) = await u.PutAsync($"/lojas/{loja.GetProperty("id").GetGuid()}/mapa", corpo);
        Assert.Equal(200, statusMapa);
        return salvo.GetProperty("id").GetGuid();
    }

    /// <summary>Lista com um item comprado (opcionalmente num mercado). Devolve (lista, item).</summary>
    private static async Task<(Guid Lista, Guid Item)> ListaComCompraAsync(UsuarioDeTeste u, Guid? mercado = null)
    {
        var (_, lista) = await u.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero", permitirDuplicada = true });
        var listaId = lista.GetProperty("resumo").GetProperty("id").GetGuid();
        var (_, item) = await u.PostAsync($"/listas/{listaId}/itens", new { nome = "Arroz", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 1 });
        var itemId = item.GetProperty("id").GetGuid();
        Assert.Equal(200, (await u.PutAsync($"/listas/{listaId}/itens/{itemId}/comprado", new { comprado = true, lojaId = mercado })).Status);
        return (listaId, itemId);
    }

    private static async Task EntrarNaFamiliaAsync(UsuarioDeTeste dono, UsuarioDeTeste quemEntra)
    {
        var (_, convite) = await dono.PostAsync("/grupo/convites");
        var (status, sessao) = await quemEntra.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        Assert.Equal(200, status);
        quemEntra.AplicarSessao(sessao);
    }

    [PostgresFact]
    public async Task Encerrar_conta_exige_o_email_e_a_senha_certos_e_enquanto_isso_nada_some()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        var u = await _api.NovoUsuarioAsync(email);

        Assert.Equal(400, (await u.PostAsync("/conta/encerrar", new { senha = "errada123", confirmacaoEmail = email })).Status);
        Assert.Equal(400, (await u.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = "outro@teste.com" })).Status);
        Assert.Equal(400, (await u.PostAsync("/conta/encerrar", new { senha = "senha1234" })).Status);

        Assert.Equal(200, (await u.GetAsync("/categorias")).Status);
        Assert.True(await NoBancoAsync(db => db.Users.AnyAsync(x => x.Id == u.Id)));
    }

    [PostgresFact]
    public async Task Encerrar_conta_apaga_o_usuario_e_todos_os_dados_do_grupo_pessoal_sem_tocar_nos_dos_outros()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        var u = await _api.NovoUsuarioAsync(email);
        var grupo = u.GrupoId;
        var mercado = await MercadoAsync(u, $"Mercado {Guid.NewGuid():N}"[..30]);
        await ListaComCompraAsync(u, mercado);
        await u.PostAsync("/modelo/itens", new { nome = "Feijão", categoriaId = await u.CategoriaAsync("Mercearia"), unidade = "Pacote", quantidade = 1 });

        var vizinho = await _api.NovoUsuarioAsync();
        var (listaDoVizinho, _) = await ListaComCompraAsync(vizinho);

        var (status, _) = await u.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = email.ToUpperInvariant() });

        Assert.Equal(204, status);
        var restos = await NoBancoAsync(async db => new
        {
            Usuario = await db.Users.CountAsync(x => x.Id == u.Id),
            Grupo = await db.Grupos.CountAsync(g => g.Id == grupo),
            Membros = await db.Membros.CountAsync(m => m.GrupoId == grupo || m.UsuarioId == u.Id),
            Categorias = await db.Categorias.CountAsync(c => c.GrupoId == grupo),
            Produtos = await db.Produtos.CountAsync(p => p.GrupoId == grupo),
            Modelo = await db.ItensDoModelo.CountAsync(i => i.GrupoId == grupo),
            Listas = await db.Listas.CountAsync(l => l.GrupoId == grupo),
            Itens = await db.ItensDasListas.CountAsync(i => i.GrupoId == grupo),
            Lojas = await db.Lojas.CountAsync(l => l.GrupoId == grupo),
            Secoes = await db.Secoes.CountAsync(s => s.GrupoId == grupo),
            Vinculos = await db.SecaoCategorias.CountAsync(v => v.GrupoId == grupo),
            Tokens = await db.TokensDeRenovacao.CountAsync(t => t.UsuarioId == u.Id),
        });
        Assert.Equal(0, restos.Usuario + restos.Grupo + restos.Membros + restos.Categorias + restos.Produtos + restos.Modelo + restos.Listas + restos.Itens + restos.Lojas + restos.Secoes + restos.Vinculos + restos.Tokens);

        // Quem já tinha sessão perde o acesso na hora, e o login não existe mais.
        Assert.Equal(401, (await u.GetAsync("/categorias")).Status);
        using var cliente = _api.CreateClient();
        Assert.Equal(401, (int)(await cliente.PostAsJsonAsync("/autenticacao/login", new { email, senha = "senha1234" })).StatusCode);

        // Os dados de outra pessoa continuam intactos.
        var (_, daVizinha) = await vizinho.GetAsync($"/listas/{listaDoVizinho}");
        Assert.Single(daVizinha.GetProperty("itens").EnumerateArray());
    }

    [PostgresFact]
    public async Task Quem_sai_da_familia_encerrando_a_conta_deixa_os_dados_da_familia_e_apaga_so_os_dele()
    {
        var dono = await _api.NovoUsuarioAsync();
        var emailDela = $"{Guid.NewGuid():N}@teste.com";
        var ela = await _api.NovoUsuarioAsync(emailDela);
        var grupoPessoalDela = ela.GrupoId;
        var familia = dono.GrupoId;
        var (listaDaFamilia, _) = await ListaComCompraAsync(dono);
        await EntrarNaFamiliaAsync(dono, ela);

        var (status, _) = await ela.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = emailDela });

        Assert.Equal(204, status);
        var estado = await NoBancoAsync(async db => new
        {
            Usuaria = await db.Users.CountAsync(x => x.Id == ela.Id),
            GrupoPessoalDela = await db.Grupos.CountAsync(g => g.Id == grupoPessoalDela),
            Familia = await db.Grupos.CountAsync(g => g.Id == familia),
            ListaDaFamilia = await db.Listas.CountAsync(l => l.Id == listaDaFamilia),
            VinculosDela = await db.Membros.CountAsync(m => m.UsuarioId == ela.Id),
            DonoSegueMembro = await db.Membros.CountAsync(m => m.GrupoId == familia && m.UsuarioId == dono.Id && m.Papel == PapelNoGrupoEnum.Administrador),
        });
        Assert.Equal(0, estado.Usuaria + estado.GrupoPessoalDela + estado.VinculosDela);
        Assert.Equal(1, estado.Familia);
        Assert.Equal(1, estado.ListaDaFamilia);
        Assert.Equal(1, estado.DonoSegueMembro);

        var (_, grupo) = await dono.GetAsync("/grupo");
        Assert.Single(grupo.GetProperty("membros").EnumerateArray());
    }

    [PostgresFact]
    public async Task Se_o_unico_administrador_encerra_a_conta_o_membro_mais_antigo_assume_a_familia()
    {
        var emailDele = $"{Guid.NewGuid():N}@teste.com";
        var dono = await _api.NovoUsuarioAsync(emailDele);
        var membro = await _api.NovoUsuarioAsync();
        var familia = dono.GrupoId;
        await ListaComCompraAsync(dono);
        await EntrarNaFamiliaAsync(dono, membro);

        Assert.Equal(204, (await dono.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = emailDele })).Status);

        var promovido = await NoBancoAsync(db => db.Membros.CountAsync(m => m.GrupoId == familia && m.UsuarioId == membro.Id && m.Papel == PapelNoGrupoEnum.Administrador));
        var listas = await NoBancoAsync(db => db.Listas.CountAsync(l => l.GrupoId == familia));
        Assert.Equal(1, promovido);
        Assert.Equal(1, listas);
    }

    [PostgresFact]
    public async Task Conta_do_google_sem_senha_propria_confirma_so_com_o_email_e_com_senha_propria_passa_a_exigir_a_senha()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        using var cliente = _api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/autenticacao/login-google", new { idToken = ValidadorDeLoginGoogleDeTeste.Token(email, "Ana Souza") });
        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var ana = new UsuarioDeTeste(cliente, sessao.GetProperty("usuario").GetProperty("id").GetGuid(), sessao);

        Assert.Equal(400, (await ana.PostAsync("/conta/encerrar", new { confirmacaoEmail = "outro@teste.com" })).Status);
        Assert.Equal(204, (await ana.PostAsync("/conta/encerrar", new { confirmacaoEmail = email })).Status);
        Assert.False(await NoBancoAsync(db => db.Users.AnyAsync(x => x.Id == ana.Id)));
    }

    [PostgresFact]
    public async Task Administrador_nao_encerra_a_propria_conta_por_aqui()
    {
        var admin = await AdminAsync();

        var (status, _) = await admin.PostAsync("/conta/encerrar", new { senha = "senha1234", confirmacaoEmail = ApiFactory.EmailDoAdmin });

        Assert.Equal(400, status);
        Assert.True(await NoBancoAsync(db => db.Users.AnyAsync(x => x.Id == admin.Id)));
    }

    [PostgresFact]
    public async Task Administrador_exclui_a_conta_de_outra_pessoa_mas_nao_a_de_administrador()
    {
        var admin = await AdminAsync();
        var comum = await _api.NovoUsuarioAsync();
        var alvo = await _api.NovoUsuarioAsync();
        var grupoDoAlvo = alvo.GrupoId;
        await ListaComCompraAsync(alvo);

        Assert.Equal(403, (await comum.DeleteAsync($"/admin/usuarios/{alvo.Id}")).Status);
        Assert.Equal(400, (await admin.DeleteAsync($"/admin/usuarios/{admin.Id}")).Status);
        Assert.Equal(404, (await admin.DeleteAsync($"/admin/usuarios/{Guid.NewGuid()}")).Status);

        Assert.Equal(204, (await admin.DeleteAsync($"/admin/usuarios/{alvo.Id}")).Status);
        Assert.False(await NoBancoAsync(db => db.Users.AnyAsync(x => x.Id == alvo.Id)));
        Assert.Equal(0, await NoBancoAsync(db => db.Listas.CountAsync(l => l.GrupoId == grupoDoAlvo)));
        Assert.Equal(401, (await alvo.GetAsync("/categorias")).Status);
    }

    [PostgresFact]
    public async Task Administrador_ve_o_mapa_e_o_exclui_sem_perder_as_compras_nem_as_copias_dos_outros()
    {
        var admin = await AdminAsync();
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var nome = $"Mercado {Guid.NewGuid():N}"[..30];
        var mercado = await MercadoAsync(autor, nome);
        var (lista, item) = await ListaComCompraAsync(autor, mercado);
        var (statusCopia, copia) = await copista.PostAsync($"/catalogo/mercados/{mercado}/copiar");
        Assert.Equal(201, statusCopia);

        // Só o administrador entra no painel de mapas.
        Assert.Equal(403, (await autor.GetAsync("/admin/mapas")).Status);
        Assert.Equal(403, (await autor.DeleteAsync($"/admin/mapas/{mercado}")).Status);

        var (statusLista, mapas) = await admin.GetAsync("/admin/mapas");
        Assert.Equal(200, statusLista);
        var linha = mapas.GetProperty("itens").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == mercado);
        Assert.Equal(nome, linha.GetProperty("nome").GetString());
        Assert.Equal(8, linha.GetProperty("secoesComNome").GetInt32());
        Assert.True(linha.GetProperty("noCatalogo").GetBoolean());
        Assert.Equal(1, linha.GetProperty("copias").GetInt32());
        Assert.False(string.IsNullOrEmpty(linha.GetProperty("donoEmail").GetString()));

        var (statusPrevia, previa) = await admin.GetAsync($"/admin/mapas/{mercado}");
        Assert.Equal(200, statusPrevia);
        Assert.Equal(8, previa.GetProperty("secoes").GetArrayLength());

        Assert.Equal(204, (await admin.DeleteAsync($"/admin/mapas/{mercado}")).Status);

        Assert.Equal(404, (await admin.GetAsync($"/admin/mapas/{mercado}")).Status);
        Assert.Equal(404, (await admin.DeleteAsync($"/admin/mapas/{mercado}")).Status);
        var (_, mercadosDoAutor) = await autor.GetAsync("/lojas");
        Assert.DoesNotContain(mercadosDoAutor.EnumerateArray(), m => m.GetProperty("id").GetGuid() == mercado);
        Assert.Equal(0, await NoBancoAsync(db => db.Secoes.CountAsync(s => s.LojaId == mercado)));

        // A compra continua, só sem o mercado.
        var (_, dados) = await autor.GetAsync($"/listas/{lista}");
        var compra = dados.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == item);
        Assert.True(compra.GetProperty("comprado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, compra.GetProperty("compradoNaLojaId").ValueKind);

        // A cópia de outro grupo é independente e fica.
        var (_, mercadosDoCopista) = await copista.GetAsync("/lojas");
        Assert.Contains(mercadosDoCopista.EnumerateArray(), m => m.GetProperty("id").GetGuid() == copia.GetProperty("id").GetGuid());
    }
}
