namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Categoria de produto de um grupo. A exclusão é lógica (ExcluidoEm) porque listas
/// antigas guardam o nome da categoria e o histórico nunca pode perder referência.
/// </summary>
public sealed class Categoria
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public bool Padrao { get; private set; }
    public DateTime? ExcluidoEm { get; private set; }

    private Categoria()
    {
    }

    public static Categoria Criar(Guid id, Guid grupoId, string nome, int ordem, bool padrao)
    {
        return new Categoria { Id = id, GrupoId = grupoId, Nome = nome, Ordem = ordem, Padrao = padrao };
    }
}
