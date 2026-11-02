namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// O que mudou, dito ao app em tempo real. Só ids e tipos: nenhum dado de negócio viaja no evento,
/// o aparelho que recebe busca o dado novo pela API (que confere o grupo e a permissão de novo).
/// </summary>
public sealed record EventoDeAlteracao(string Tipo, Guid? ListaId = null, Guid? ItemId = null, Guid? PorUsuarioId = null)
{
    public const string Lista = "lista";
    public const string Catalogo = "catalogo";
    public const string Lojas = "lojas";
    public const string Membros = "membros";
    public const string GrupoAlterado = "grupoAlterado";
}

/// <summary>
/// Avisa os outros aparelhos do grupo que algo mudou. A falha em avisar nunca deve derrubar a operação
/// (o dado já foi gravado): quem implementa só registra o erro.
/// </summary>
public interface INotificador
{
    Task PublicarNoGrupoAsync(Guid grupoId, EventoDeAlteracao evento, CancellationToken ct);
    Task PublicarParaUsuarioAsync(Guid usuarioId, EventoDeAlteracao evento, CancellationToken ct);
}

public interface IGeradorDeCodigo
{
    /// <summary>Código aleatório de 8 caracteres (sem letras ambíguas como O/0 e I/1), já normalizado.</summary>
    string Gerar();
}
