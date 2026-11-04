using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Notificacoes;

/// <summary>Chave pública que o navegador usa para assinar os avisos. Nula quando o servidor não tem avisos ligados.</summary>
public sealed class ObterChaveDePush
{
    private readonly INotificadorDePush _push;

    public ObterChaveDePush(INotificadorDePush push)
    {
        _push = push;
    }

    public ChaveDePushResposta Executar()
    {
        return _push.Habilitado && _push.ChavePublica is { } chave ? new ChaveDePushResposta(chave) : throw new ErroDeNaoEncontrado("Os avisos não estão ligados neste servidor.");
    }
}

public sealed class AssinarPush
{
    private readonly IAssinaturaDePushRepository _assinaturas;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public AssinarPush(IAssinaturaDePushRepository assinaturas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _assinaturas = assinaturas;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(AssinarPushRequisicao requisicao, CancellationToken ct)
    {
        var endpoint = requisicao.Endpoint?.Trim();
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2000 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new ErroDeValidacao("Assinatura de avisos inválida.");
        if (string.IsNullOrWhiteSpace(requisicao.P256dh) || string.IsNullOrWhiteSpace(requisicao.Auth) || requisicao.P256dh.Length > 200 || requisicao.Auth.Length > 100) throw new ErroDeValidacao("Assinatura de avisos inválida.");

        // O endereço é único: o mesmo aparelho só tem uma linha, que muda de dono se outra conta entrar nele.
        var existente = await _assinaturas.ObterPorEndpointAsync(endpoint, ct);
        if (existente is null) await _assinaturas.AdicionarAsync(AssinaturaDePush.Criar(Guid.CreateVersion7(), _usuario.UsuarioId, endpoint, requisicao.P256dh, requisicao.Auth, _relogio.AgoraUtc), ct);
        else existente.Atualizar(_usuario.UsuarioId, requisicao.P256dh, requisicao.Auth);

        await _unidade.SalvarAsync(ct);
    }
}

public sealed class CancelarPush
{
    private readonly IAssinaturaDePushRepository _assinaturas;
    private readonly IUsuarioAtual _usuario;

    public CancelarPush(IAssinaturaDePushRepository assinaturas, IUsuarioAtual usuario)
    {
        _assinaturas = assinaturas;
        _usuario = usuario;
    }

    /// <summary>Só apaga a assinatura do próprio usuário: ninguém desliga os avisos de outra pessoa.</summary>
    public async Task ExecutarAsync(CancelarPushRequisicao requisicao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Endpoint)) throw new ErroDeValidacao("Assinatura de avisos inválida.");
        await _assinaturas.RemoverPorEndpointAsync(requisicao.Endpoint.Trim(), _usuario.UsuarioId, ct);
    }
}
