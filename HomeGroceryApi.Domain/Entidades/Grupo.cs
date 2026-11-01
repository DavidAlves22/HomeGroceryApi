namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Dono de todos os dados (categorias, produtos, listas). Todo usuário nasce com um
/// grupo pessoal; compartilhar com a família é só adicionar membros a um grupo.
/// </summary>
public sealed class Grupo
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public bool Pessoal { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private Grupo()
    {
    }

    public static Grupo CriarPessoal(Guid id, string nomeDoUsuario, DateTime agora)
    {
        return new Grupo { Id = id, Nome = $"Casa de {nomeDoUsuario}", Pessoal = true, CriadoEm = agora };
    }
}
