using System.Net;
using System.Text.Json;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Interfaces;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>Chaves VAPID (identificam este servidor perante o serviço de push do navegador). Sem elas, os avisos ficam desligados.</summary>
public sealed class PushOpcoes
{
    public const string Secao = "Push";
    public string ChavePublica { get; set; } = string.Empty;
    public string ChavePrivada { get; set; } = string.Empty;
    public string Assunto { get; set; } = "mailto:contato@homegrocery.app";
}

public sealed class NotificadorDePush : INotificadorDePush
{
    private readonly PushOpcoes _opcoes;
    private readonly PushServiceClient _cliente;
    private readonly IAssinaturaDePushRepository _assinaturas;
    private readonly ILogger<NotificadorDePush> _log;

    public NotificadorDePush(IOptions<PushOpcoes> opcoes, PushServiceClient cliente, IAssinaturaDePushRepository assinaturas, ILogger<NotificadorDePush> log)
    {
        _opcoes = opcoes.Value;
        _cliente = cliente;
        _assinaturas = assinaturas;
        _log = log;
        if (Habilitado) _cliente.DefaultAuthentication = new VapidAuthentication(_opcoes.ChavePublica, _opcoes.ChavePrivada) { Subject = _opcoes.Assunto };
    }

    public bool Habilitado => !string.IsNullOrWhiteSpace(_opcoes.ChavePublica) && !string.IsNullOrWhiteSpace(_opcoes.ChavePrivada);

    public string? ChavePublica => Habilitado ? _opcoes.ChavePublica : null;

    public async Task EnviarAsync(IReadOnlyCollection<Guid> usuarios, NotificacaoDePush notificacao, CancellationToken ct)
    {
        if (!Habilitado || usuarios.Count == 0) return;

        var conteudo = JsonSerializer.Serialize(new { titulo = notificacao.Titulo, corpo = notificacao.Corpo, url = notificacao.Url, tag = notificacao.Tag }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var assinatura in await _assinaturas.ListarDosUsuariosAsync(usuarios, ct))
        {
            try
            {
                var inscricao = new PushSubscription { Endpoint = assinatura.Endpoint, Keys = new Dictionary<string, string> { ["p256dh"] = assinatura.P256dh, ["auth"] = assinatura.Auth } };
                await _cliente.RequestPushMessageDeliveryAsync(inscricao, new PushMessage(conteudo) { Topic = notificacao.Tag.Length <= 32 ? notificacao.Tag : null, TimeToLive = 60 * 60 * 12 }, ct);
            }
            catch (PushServiceClientException erro) when (erro.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                // O navegador cancelou a assinatura (app desinstalado, permissão revogada): não adianta tentar de novo.
                await _assinaturas.RemoverPorEndpointAsync(assinatura.Endpoint, null, ct);
            }
            catch (Exception erro)
            {
                _log.LogWarning(erro, "Falha ao enviar aviso para uma assinatura");
            }
        }
    }
}
