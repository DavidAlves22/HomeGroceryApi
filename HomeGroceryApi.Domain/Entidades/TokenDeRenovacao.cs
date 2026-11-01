namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Sessão renovável. Só o hash do token é guardado: vazamento do banco não vira sessão.
/// </summary>
public sealed class TokenDeRenovacao
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Hash { get; private set; } = string.Empty;
    public DateTime ExpiraEm { get; private set; }
    public DateTime? RevogadoEm { get; private set; }

    private TokenDeRenovacao()
    {
    }

    public static TokenDeRenovacao Criar(Guid id, Guid usuarioId, string hash, DateTime expiraEm)
    {
        return new TokenDeRenovacao { Id = id, UsuarioId = usuarioId, Hash = hash, ExpiraEm = expiraEm };
    }

    public bool EstaValido(DateTime agora)
    {
        return RevogadoEm is null && ExpiraEm > agora;
    }

    public void Revogar(DateTime agora)
    {
        RevogadoEm ??= agora;
    }
}
