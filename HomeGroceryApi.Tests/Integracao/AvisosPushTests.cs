using System.Text.Json;
using HomeGroceryApi.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGroceryApi.Tests.Integracao;

[Collection("api")]
public class AvisosPushTests
{
    private readonly ApiFactory _api;

    public AvisosPushTests(ApiFactory api)
    {
        _api = api;
    }

    private static object Assinatura(string endpoint) => new { endpoint, p256dh = "BChaveDeTeste", auth = "authDeTeste" };

    private async Task<(UsuarioDeTeste Eu, UsuarioDeTeste Esposa)> FamiliaAsync()
    {
        var eu = await _api.NovoUsuarioAsync();
        var esposa = await _api.NovoUsuarioAsync();
        var (_, convite) = await eu.PostAsync("/grupo/convites");
        var (_, sessao) = await esposa.PostAsync("/grupo/entrar", new { codigo = convite.GetProperty("codigo").GetString() });
        esposa.AplicarSessao(sessao);
        return (eu, esposa);
    }

    private IEnumerable<(IReadOnlyCollection<Guid> Usuarios, HomeGroceryApi.Application.Interfaces.NotificacaoDePush Notificacao)> AvisosDaLista(Guid lista)
        => _api.Avisos.Enviados.Where(e => e.Notificacao.Tag.EndsWith(lista.ToString("N")));

    [PostgresFact]
    public async Task Pedido_de_item_e_finalizacao_avisam_so_os_outros_do_grupo_e_criar_a_lista_nao_avisa()
    {
        var (eu, esposa) = await FamiliaAsync();
        var (_, criada) = await eu.PostAsync("/listas", new { competencia = "2026-11", origem = "Zero" });
        var lista = criada.GetProperty("resumo").GetProperty("id").GetGuid();
        Assert.Empty(AvisosDaLista(lista));

        await esposa.PostAsync($"/listas/{lista}/itens", new { nome = "Café", categoriaId = await esposa.CategoriaAsync("Mercearia"), unidade = "Unidade", quantidade = 1 });
        var pedido = Assert.Single(AvisosDaLista(lista));
        Assert.Equal([eu.Id], pedido.Usuarios);
        Assert.Equal("Maria pediu um item", pedido.Notificacao.Titulo);
        Assert.Equal("Café", pedido.Notificacao.Corpo);
        Assert.Equal("/lista", pedido.Notificacao.Url);

        // Eu também adiciono: o aviso vai para a esposa, nunca para mim.
        await eu.PostAsync($"/listas/{lista}/itens", new { nome = "Leite", categoriaId = await eu.CategoriaAsync("Mercearia"), unidade = "Unidade", quantidade = 1 });
        Assert.Equal([esposa.Id], AvisosDaLista(lista).Last().Usuarios);

        await esposa.PostAsync($"/listas/{lista}/finalizar");
        var final = AvisosDaLista(lista).Last();
        Assert.Equal("Compra finalizada", final.Notificacao.Titulo);
        Assert.Contains("finalizou a compra de", final.Notificacao.Corpo);
        Assert.Equal([eu.Id], final.Usuarios);
    }

    [PostgresFact]
    public async Task Assinar_guarda_o_aparelho_troca_de_dono_e_so_o_dono_cancela()
    {
        var a = await _api.NovoUsuarioAsync();
        var b = await _api.NovoUsuarioAsync();
        var endpoint = $"https://push.exemplo.com/{Guid.NewGuid():N}";
        // Cada leitura em um escopo novo: o EF guarda em cache o que já leu na mesma conexão.
        async Task<HomeGroceryApi.Domain.Entidades.AssinaturaDePush?> Ler()
        {
            using var escopo = _api.Services.CreateScope();
            return await escopo.ServiceProvider.GetRequiredService<IAssinaturaDePushRepository>().ObterPorEndpointAsync(endpoint, default);
        }

        Assert.Equal(204, (await a.PutAsync("/notificacoes/assinatura", Assinatura(endpoint))).Status);
        Assert.Equal(a.Id, (await Ler())!.UsuarioId);

        // Outra pessoa entra no mesmo aparelho: a assinatura muda de dono, sem duplicar.
        Assert.Equal(204, (await b.PutAsync("/notificacoes/assinatura", Assinatura(endpoint))).Status);
        Assert.Equal(b.Id, (await Ler())!.UsuarioId);

        // Quem não é o dono não cancela; o dono cancela.
        await a.PostAsync("/notificacoes/assinatura/cancelar", new { endpoint });
        Assert.NotNull(await Ler());
        await b.PostAsync("/notificacoes/assinatura/cancelar", new { endpoint });
        Assert.Null(await Ler());
    }

    [PostgresFact]
    public async Task Assinatura_invalida_e_recusada_e_a_chave_publica_e_entregue()
    {
        var u = await _api.NovoUsuarioAsync();

        Assert.Equal(400, (await u.PutAsync("/notificacoes/assinatura", Assinatura("http://inseguro.exemplo.com/x"))).Status);
        Assert.Equal(400, (await u.PutAsync("/notificacoes/assinatura", new { endpoint = "https://push.exemplo.com/x", p256dh = "", auth = "a" })).Status);

        var (status, chave) = await u.GetAsync("/notificacoes/chave");
        Assert.Equal(200, status);
        Assert.Equal("chave-publica-de-teste", chave.GetProperty("chavePublica").GetString());
    }
}
