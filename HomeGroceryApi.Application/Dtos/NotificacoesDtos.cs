namespace HomeGroceryApi.Application.Dtos;

public sealed record ChaveDePushResposta(string ChavePublica);

/// <summary>Assinatura que o navegador devolve ao aceitar os avisos (`PushSubscription.toJSON()`).</summary>
public sealed record AssinarPushRequisicao(string? Endpoint, string? P256dh, string? Auth);

public sealed record CancelarPushRequisicao(string? Endpoint);
