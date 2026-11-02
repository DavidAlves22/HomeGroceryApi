using HomeGroceryApi.Domain.Excecoes;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Convite para entrar em um grupo familiar: um código de uso único com prazo de validade.
/// Quem tem o código entra no grupo; por isso ele expira e nunca pode ser reaproveitado.
/// </summary>
public sealed class Convite
{
    public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }

    /// <summary>Código normalizado (maiúsculas, sem hífen), como é guardado e comparado.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public Guid CriadoPorUsuarioId { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public Guid? UsadoPorUsuarioId { get; private set; }
    public DateTime? UsadoEm { get; private set; }

    private Convite()
    {
    }

    public static Convite Criar(Guid id, Guid grupoId, string codigo, Guid usuarioId, DateTime agora)
    {
        return new Convite { Id = id, GrupoId = grupoId, Codigo = codigo, CriadoPorUsuarioId = usuarioId, CriadoEm = agora, ExpiraEm = agora + Validade };
    }

    public bool EstaValido(DateTime agora) => UsadoEm is null && ExpiraEm > agora;

    public void Usar(Guid usuarioId, DateTime agora)
    {
        if (!EstaValido(agora)) throw new ErroDeDominio("Este convite expirou ou já foi usado.");
        UsadoPorUsuarioId = usuarioId;
        UsadoEm = agora;
    }

    /// <summary>Tira hífen e espaços e põe em maiúsculas: "k7m2-xq9p" e "K7M2XQ9P" são o mesmo código.</summary>
    public static string NormalizarCodigo(string? texto)
    {
        return new string((texto ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
