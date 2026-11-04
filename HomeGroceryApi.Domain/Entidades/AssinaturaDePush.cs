namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Um aparelho/navegador que aceitou receber avisos (Web Push). O endereço (`Endpoint`) é único e é dado
/// pelo próprio navegador; `P256dh` e `Auth` são as chaves para cifrar a mensagem. O mesmo usuário pode ter
/// várias assinaturas (celular e computador).
/// </summary>
public sealed class AssinaturaDePush
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public DateTime CriadoEm { get; private set; }

    private AssinaturaDePush()
    {
    }

    public static AssinaturaDePush Criar(Guid id, Guid usuarioId, string endpoint, string p256dh, string auth, DateTime agora)
    {
        return new AssinaturaDePush { Id = id, UsuarioId = usuarioId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, CriadoEm = agora };
    }

    /// <summary>O mesmo aparelho pode trocar de usuário (saiu de uma conta e entrou em outra) ou renovar as chaves.</summary>
    public void Atualizar(Guid usuarioId, string p256dh, string auth)
    {
        UsuarioId = usuarioId;
        P256dh = p256dh;
        Auth = auth;
    }
}
