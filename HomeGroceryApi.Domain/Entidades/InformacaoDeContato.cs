namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Uma informação que o administrador mostra a todos os usuários no card de contato (e-mail, chave Pix, link...). O
/// administrador decide quantas e quais; a descrição é opcional. Fica no banco para mudar pelo painel, sem publicar de novo.
/// </summary>
public sealed class InformacaoDeContato
{
    public const int TituloMaximo = 60;
    public const int DescricaoMaxima = 200;
    public const int ValorMaximo = 300;

    public Guid Id { get; private set; }
    public string Titulo { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public string Valor { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private InformacaoDeContato()
    {
    }

    public static InformacaoDeContato Criar(Guid id, string titulo, string? descricao, string valor, int ordem, DateTime agora)
    {
        return new InformacaoDeContato { Id = id, Titulo = titulo, Descricao = descricao, Valor = valor, Ordem = ordem, CriadoEm = agora };
    }

    public void Alterar(string titulo, string? descricao, string valor)
    {
        Titulo = titulo;
        Descricao = descricao;
        Valor = valor;
    }
}
