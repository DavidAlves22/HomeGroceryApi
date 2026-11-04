namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Código de 6 dígitos enviado por e-mail para redefinir a senha de quem a esqueceu. Só o hash fica guardado (vazar o
/// banco não entrega códigos válidos), vale por pouco tempo, serve uma vez e trava depois de poucas tentativas erradas
/// (6 dígitos são só um milhão de combinações: sem o limite, dava para adivinhar).
/// </summary>
public sealed class CodigoDeRecuperacao
{
    public const int MaximoDeTentativas = 5;

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string CodigoHash { get; private set; } = string.Empty;
    public DateTime CriadoEm { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public int Tentativas { get; private set; }
    public DateTime? UsadoEm { get; private set; }

    private CodigoDeRecuperacao()
    {
    }

    public static CodigoDeRecuperacao Criar(Guid id, Guid usuarioId, string codigoHash, DateTime agora, DateTime expiraEm)
    {
        return new CodigoDeRecuperacao { Id = id, UsuarioId = usuarioId, CodigoHash = codigoHash, CriadoEm = agora, ExpiraEm = expiraEm };
    }

    public bool EstaAtivo(DateTime agora) => UsadoEm is null && ExpiraEm > agora && Tentativas < MaximoDeTentativas;

    public void RegistrarTentativaErrada()
    {
        Tentativas++;
    }

    public void MarcarComoUsado(DateTime agora)
    {
        UsadoEm ??= agora;
    }
}
