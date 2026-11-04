using System.Net;
using System.Security.Cryptography;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Infrastructure.Servicos;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeGroceryApi.Tests.Dominio;

public class NotificadorDePushTests
{
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Gera chaves VAPID no formato esperado: ponto público (65 bytes) e chave privada (32 bytes) em base64 de URL.</summary>
    private static PushOpcoes NovasChaves()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(true);
        var publica = new byte[] { 4 }.Concat(p.Q.X!).Concat(p.Q.Y!).ToArray();
        return new PushOpcoes { ChavePublica = Base64Url(publica), ChavePrivada = Base64Url(p.D!), Assunto = "mailto:teste@exemplo.com" };
    }

    private sealed class ServicoDePushFalso : HttpMessageHandler
    {
        public List<HttpRequestMessage> Pedidos { get; } = [];
        public HttpStatusCode Resposta { get; set; } = HttpStatusCode.Created;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add(request);
            return Task.FromResult(new HttpResponseMessage(Resposta));
        }
    }

    private sealed class Assinaturas : IAssinaturaDePushRepository
    {
        public List<AssinaturaDePush> Itens { get; } = [];
        public List<string> Removidas { get; } = [];

        public Task<AssinaturaDePush?> ObterPorEndpointAsync(string endpoint, CancellationToken ct) => Task.FromResult(Itens.FirstOrDefault(a => a.Endpoint == endpoint));
        public Task AdicionarAsync(AssinaturaDePush assinatura, CancellationToken ct) { Itens.Add(assinatura); return Task.CompletedTask; }
        public Task<List<AssinaturaDePush>> ListarDosUsuariosAsync(IReadOnlyCollection<Guid> usuarios, CancellationToken ct) => Task.FromResult(Itens.Where(a => usuarios.Contains(a.UsuarioId)).ToList());
        public Task RemoverPorEndpointAsync(string endpoint, Guid? usuarioId, CancellationToken ct) { Removidas.Add(endpoint); return Task.CompletedTask; }
    }

    /// <summary>Assinatura como a de um navegador de verdade: chave pública P-256 (65 bytes) e segredo de autenticação (16 bytes).</summary>
    private static AssinaturaDePush AssinaturaDoNavegador(Guid usuario, string endpoint)
    {
        using var ec = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(false);
        var publica = new byte[] { 4 }.Concat(p.Q.X!).Concat(p.Q.Y!).ToArray();
        return AssinaturaDePush.Criar(Guid.NewGuid(), usuario, endpoint, Base64Url(publica), Base64Url(RandomNumberGenerator.GetBytes(16)), DateTime.UtcNow);
    }

    private static (NotificadorDePush Notificador, ServicoDePushFalso Servico, Assinaturas Repositorio) Montar(PushOpcoes opcoes)
    {
        var servico = new ServicoDePushFalso();
        var repositorio = new Assinaturas();
        var cliente = new PushServiceClient(new HttpClient(servico));
        return (new NotificadorDePush(Options.Create(opcoes), cliente, repositorio, NullLogger<NotificadorDePush>.Instance), servico, repositorio);
    }

    [Fact]
    public async Task Envia_aviso_cifrado_e_assinado_com_a_chave_do_servidor_para_cada_aparelho_do_usuario()
    {
        var (notificador, servico, repositorio) = Montar(NovasChaves());
        var usuario = Guid.NewGuid();
        repositorio.Itens.Add(AssinaturaDoNavegador(usuario, "https://push.exemplo.com/celular"));
        repositorio.Itens.Add(AssinaturaDoNavegador(usuario, "https://push.exemplo.com/notebook"));
        repositorio.Itens.Add(AssinaturaDoNavegador(Guid.NewGuid(), "https://push.exemplo.com/de-outro"));

        await notificador.EnviarAsync([usuario], new NotificacaoDePush("Ana pediu um item", "Café", "/lista", "pedido-1"), default);

        Assert.True(notificador.Habilitado);
        Assert.Equal(2, servico.Pedidos.Count);
        Assert.All(servico.Pedidos, pedido =>
        {
            Assert.Equal(HttpMethod.Post, pedido.Method);
            Assert.Equal("vapid", pedido.Headers.Authorization!.Scheme);
            Assert.Contains("aes128gcm", pedido.Content!.Headers.ContentEncoding);
        });
        Assert.Contains(servico.Pedidos, p => p.RequestUri!.AbsoluteUri == "https://push.exemplo.com/celular");
        Assert.DoesNotContain(servico.Pedidos, p => p.RequestUri!.AbsoluteUri.EndsWith("de-outro"));
    }

    [Fact]
    public async Task Assinatura_cancelada_pelo_navegador_e_apagada_e_sem_chaves_nada_e_enviado()
    {
        var (notificador, servico, repositorio) = Montar(NovasChaves());
        var usuario = Guid.NewGuid();
        repositorio.Itens.Add(AssinaturaDoNavegador(usuario, "https://push.exemplo.com/morto"));
        servico.Resposta = HttpStatusCode.Gone;

        await notificador.EnviarAsync([usuario], new NotificacaoDePush("t", "c", "/lista", "x"), default);
        Assert.Equal(["https://push.exemplo.com/morto"], repositorio.Removidas);

        var (desligado, servicoDesligado, repositorioDesligado) = Montar(new PushOpcoes());
        repositorioDesligado.Itens.Add(AssinaturaDoNavegador(usuario, "https://push.exemplo.com/x"));
        await desligado.EnviarAsync([usuario], new NotificacaoDePush("t", "c", "/lista", "x"), default);
        Assert.False(desligado.Habilitado);
        Assert.Null(desligado.ChavePublica);
        Assert.Empty(servicoDesligado.Pedidos);
    }
}
