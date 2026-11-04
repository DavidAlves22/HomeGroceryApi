using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class CatalogoDeMapasTests
{
    private readonly ApiFactory _api;

    public CatalogoDeMapasTests(ApiFactory api)
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

    /// <summary>Nome único: o banco é compartilhado entre os testes, então cada teste procura só o seu mercado.</summary>
    private static string NomeUnico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}"[..40];

    private static async Task<Guid> MercadoMapeadoAsync(UsuarioDeTeste u, string nome, string categoriaExtra = "Cervejas", string cidade = "São Paulo")
    {
        var (status, loja) = await u.PostAsync("/lojas", new { nome, cidade });
        Assert.Equal(201, status);
        var mercearia = await u.CategoriaAsync("Mercearia");
        var limpeza = await u.CategoriaAsync("Limpeza");
        var extra = await u.CategoriaAsync(categoriaExtra);
        var (statusMapa, salvo) = await u.PutAsync($"/lojas/{loja.GetProperty("id").GetGuid()}/mapa", Mapa(nome, loja.GetProperty("versao").GetInt32(),
            Secao("Gôndola 1", Area(8, 8, 2, 7), [mercearia, extra], [limpeza], "Gôndola 1B", dividida: true),
            Secao("Balcão", Area(3, 3, 8, 2), [mercearia])));
        Assert.Equal(200, statusMapa);
        return salvo.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> CatalogoAsync(UsuarioDeTeste u, string busca)
    {
        var (status, itens) = await u.GetAsync($"/catalogo/mercados?busca={Uri.EscapeDataString(busca)}");
        Assert.Equal(200, status);
        return itens.EnumerateArray().ToArray();
    }

    [PostgresFact]
    public async Task O_catalogo_mostra_mapas_de_outros_grupos_e_nunca_os_proprios_nem_os_vazios()
    {
        var autor = await _api.NovoUsuarioAsync();
        var outro = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Catalogo");
        await MercadoMapeadoAsync(autor, nome);
        var vazio = NomeUnico("Mercado Vazio");
        await autor.PostAsync("/lojas", new { nome = vazio, cidade = "São Paulo" });

        var visto = Assert.Single(await CatalogoAsync(outro, nome));
        Assert.Equal(nome, visto.GetProperty("nome").GetString());
        Assert.Equal(2, visto.GetProperty("secoes").GetInt32());
        Assert.Equal(0, visto.GetProperty("copias").GetInt32());

        Assert.Empty(await CatalogoAsync(autor, nome));
        Assert.Empty(await CatalogoAsync(outro, vazio));
        Assert.Single(await CatalogoAsync(outro, nome.ToUpperInvariant()));
        Assert.Empty(await CatalogoAsync(outro, "%"));
    }

    [PostgresFact]
    public async Task O_detalhe_traz_as_categorias_pelo_nome_e_sem_dados_de_quem_criou()
    {
        var autor = await _api.NovoUsuarioAsync();
        var outro = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Detalhe");
        await MercadoMapeadoAsync(autor, nome);
        var id = (await CatalogoAsync(outro, nome))[0].GetProperty("id").GetGuid();

        var (status, detalhe) = await outro.GetAsync($"/catalogo/mercados/{id}");

        Assert.Equal(200, status);
        var gondola = detalhe.GetProperty("secoes").EnumerateArray().First(s => s.GetProperty("dividida").GetBoolean());
        Assert.Equal(["Cervejas", "Mercearia"], gondola.GetProperty("categoriasLadoA").EnumerateArray().Select(c => c.GetString()!).Order().ToArray());
        Assert.Equal(["Limpeza"], gondola.GetProperty("categoriasLadoB").EnumerateArray().Select(c => c.GetString()!).ToArray());
        Assert.DoesNotContain("grupo", detalhe.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(404, (await outro.GetAsync($"/catalogo/mercados/{Guid.NewGuid()}")).Status);

        using var anonimo = _api.CreateClient();
        Assert.Equal(401, (int)(await anonimo.GetAsync("/catalogo/mercados")).StatusCode);
    }

    [PostgresFact]
    public async Task Copiar_cria_um_mercado_independente_no_meu_grupo_com_as_categorias_pelo_nome()
    {
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Copia");
        var autorLojaId = await MercadoMapeadoAsync(autor, nome);
        var catalogoId = (await CatalogoAsync(copista, nome))[0].GetProperty("id").GetGuid();

        var (status, copia) = await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar");

        Assert.Equal(201, status);
        Assert.Equal(nome, copia.GetProperty("nome").GetString());
        Assert.Equal(2, copia.GetProperty("secoes").GetArrayLength());
        var copiaId = copia.GetProperty("id").GetGuid();
        Assert.NotEqual(autorLojaId, copiaId);
        var gondola = copia.GetProperty("secoes").EnumerateArray().First(s => s.GetProperty("dividida").GetBoolean());
        Assert.Contains(await copista.CategoriaAsync("Cervejas"), gondola.GetProperty("categoriasLadoA").EnumerateArray().Select(c => c.GetGuid()));
        Assert.Equal(1, (await copista.GetAsync("/lojas")).Corpo.GetArrayLength());

        // Editar a cópia nunca mexe no original.
        var (statusEdicao, _) = await copista.PutAsync($"/lojas/{copiaId}/mapa", Mapa(nome, copia.GetProperty("versao").GetInt32(), Secao("Só uma", Area(1, 1, 3, 2))));
        Assert.Equal(200, statusEdicao);
        var (_, original) = await autor.GetAsync($"/lojas/{autorLojaId}");
        Assert.Equal(2, original.GetProperty("secoes").GetArrayLength());

        // O autor não consegue abrir nem alterar a cópia de outro grupo.
        Assert.Equal(404, (await autor.GetAsync($"/lojas/{copiaId}")).Status);
        Assert.Equal(404, (await autor.PutAsync($"/lojas/{copiaId}/mapa", Mapa(nome, 2, Secao("X", Area(1, 1, 2, 2))))).Status);
    }

    [PostgresFact]
    public async Task Copiar_cria_as_categorias_que_o_grupo_nao_tem_e_o_nome_repetido_ganha_numero()
    {
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var categoriaNova = $"Corredor {Guid.NewGuid():N}"[..30];
        Assert.Equal(201, (await autor.PostAsync("/categorias", new { nome = categoriaNova })).Status);
        var nome = NomeUnico("Mercado Categorias");
        await MercadoMapeadoAsync(autor, nome, categoriaNova);
        var catalogoId = (await CatalogoAsync(copista, nome))[0].GetProperty("id").GetGuid();

        Assert.DoesNotContain((await copista.GetAsync("/categorias")).Corpo.EnumerateArray(), c => c.GetProperty("nome").GetString() == categoriaNova);
        Assert.Equal(201, (await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar")).Status);
        Assert.Contains((await copista.GetAsync("/categorias")).Corpo.EnumerateArray(), c => c.GetProperty("nome").GetString() == categoriaNova);

        var (_, segunda) = await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar");
        Assert.Equal($"{nome} (2)", segunda.GetProperty("nome").GetString());
    }

    [PostgresFact]
    public async Task Copia_so_entra_no_catalogo_depois_de_editada_e_conta_quantas_vezes_foi_copiada()
    {
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var terceiro = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Repetido");
        await MercadoMapeadoAsync(autor, nome);
        var catalogoId = (await CatalogoAsync(copista, nome))[0].GetProperty("id").GetGuid();

        var (_, copia) = await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar");

        // Cópia intocada não aparece (senão o catálogo encheria de repetidos); o original passa a contar a cópia.
        var itens = await CatalogoAsync(terceiro, nome);
        Assert.Single(itens);
        Assert.Equal(1, itens[0].GetProperty("copias").GetInt32());

        var nomeEditado = NomeUnico("Mercado Repetido Ed");
        await copista.PutAsync($"/lojas/{copia.GetProperty("id").GetGuid()}/mapa", Mapa(nomeEditado, copia.GetProperty("versao").GetInt32(), Secao("Nova", Area(1, 1, 4, 2))));
        Assert.Single(await CatalogoAsync(terceiro, nomeEditado));
    }

    [PostgresFact]
    public async Task Parar_de_compartilhar_ou_excluir_tira_do_catalogo_mas_nao_das_copias_ja_feitas()
    {
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var outro = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Privado");
        var lojaId = await MercadoMapeadoAsync(autor, nome);
        var catalogoId = (await CatalogoAsync(copista, nome))[0].GetProperty("id").GetGuid();
        await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar");

        var (status, resumo) = await autor.PutAsync($"/lojas/{lojaId}/compartilhamento", new { compartilhada = false });
        Assert.Equal(200, status);
        Assert.False(resumo.GetProperty("compartilhada").GetBoolean());
        Assert.Empty(await CatalogoAsync(outro, nome));
        Assert.Equal(404, (await outro.GetAsync($"/catalogo/mercados/{catalogoId}")).Status);
        Assert.Equal(404, (await outro.PostAsync($"/catalogo/mercados/{catalogoId}/copiar")).Status);
        Assert.Equal(1, (await copista.GetAsync("/lojas")).Corpo.GetArrayLength());

        await autor.PutAsync($"/lojas/{lojaId}/compartilhamento", new { compartilhada = true });
        Assert.Single(await CatalogoAsync(outro, nome));

        await autor.DeleteAsync($"/lojas/{lojaId}");
        Assert.Empty(await CatalogoAsync(outro, nome));

        // Outro grupo não liga/desliga o compartilhamento do que não é seu.
        Assert.Equal(404, (await outro.PutAsync($"/lojas/{lojaId}/compartilhamento", new { compartilhada = false })).Status);
    }

    [PostgresFact]
    public async Task A_cidade_e_obrigatoria_ao_criar_e_ao_salvar_e_o_filtro_ignora_acento_e_maiuscula()
    {
        var autor = await _api.NovoUsuarioAsync();
        var outro = await _api.NovoUsuarioAsync();

        Assert.Equal(400, (await autor.PostAsync("/lojas", new { nome = "Sem cidade" })).Status);
        Assert.Equal(400, (await autor.PostAsync("/lojas", new { nome = "Cidade vazia", cidade = "   " })).Status);
        Assert.Equal(400, (await autor.PostAsync("/lojas", new { nome = "Cidade enorme", cidade = new string('x', 81) })).Status);

        var prefixo = NomeUnico("Mercado Cidade");
        await MercadoMapeadoAsync(autor, prefixo + " SP", cidade: "São Paulo");
        await MercadoMapeadoAsync(autor, prefixo + " CP", cidade: "Campinas");

        Assert.Equal(2, (await CatalogoAsync(outro, prefixo)).Length);
        var (_, filtrado) = await outro.GetAsync($"/catalogo/mercados?busca={Uri.EscapeDataString(prefixo)}&cidade={Uri.EscapeDataString("sao PAULO")}");
        var unico = Assert.Single(filtrado.EnumerateArray());
        Assert.Equal("São Paulo", unico.GetProperty("cidade").GetString());
        Assert.Equal(prefixo + " SP", unico.GetProperty("nome").GetString());
        var (_, parcial) = await outro.GetAsync($"/catalogo/mercados?busca={Uri.EscapeDataString(prefixo)}&cidade=camp");
        Assert.Equal(prefixo + " CP", Assert.Single(parcial.EnumerateArray()).GetProperty("nome").GetString());
        var (_, nenhum) = await outro.GetAsync($"/catalogo/mercados?busca={Uri.EscapeDataString(prefixo)}&cidade=Recife");
        Assert.Empty(nenhum.EnumerateArray());
    }

    [PostgresFact]
    public async Task A_copia_herda_a_cidade_e_a_cidade_pode_ser_corrigida_ao_salvar_o_mapa()
    {
        var autor = await _api.NovoUsuarioAsync();
        var copista = await _api.NovoUsuarioAsync();
        var nome = NomeUnico("Mercado Heranca");
        await MercadoMapeadoAsync(autor, nome, cidade: "Araras");
        var catalogoId = (await CatalogoAsync(copista, nome))[0].GetProperty("id").GetGuid();

        var (_, copia) = await copista.PostAsync($"/catalogo/mercados/{catalogoId}/copiar");
        Assert.Equal("Araras", copia.GetProperty("cidade").GetString());

        var copiaId = copia.GetProperty("id").GetGuid();
        var (status, salvo) = await copista.PutAsync($"/lojas/{copiaId}/mapa", new { nome, cidade = " Limeira ", largura = 30, altura = 20, entrada = new { x = 2, y = 18 }, caixas = Area(24, 17, 5, 2), secoes = new[] { Secao("Só", Area(1, 1, 3, 2)) }, versao = copia.GetProperty("versao").GetInt32() });
        Assert.Equal(200, status);
        Assert.Equal("Limeira", salvo.GetProperty("cidade").GetString());
        Assert.Equal(400, (await copista.PutAsync($"/lojas/{copiaId}/mapa", new { nome, cidade = " ", largura = 30, altura = 20, entrada = new { x = 2, y = 18 }, caixas = Area(24, 17, 5, 2), secoes = new[] { Secao("Só", Area(1, 1, 3, 2)) }, versao = salvo.GetProperty("versao").GetInt32() })).Status);
    }
}
