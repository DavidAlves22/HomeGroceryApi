using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Prateleira ou gôndola de um mercado. Uma gôndola "dividida" tem dois lados (ex.: Molhos de um lado e
/// Copos descartáveis do outro): quem pega um item do lado A anda no corredor do lado A. As categorias de
/// cada lado ficam em SecaoCategoria. O mapa é sempre salvo por inteiro, então a seção não tem edição parcial.
/// </summary>
public sealed class Secao
{
    public Guid Id { get; private set; }
    public Guid LojaId { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? NomeLadoB { get; private set; }
    public bool Dividida { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Largura { get; private set; }
    public int Altura { get; private set; }

    public AreaDoMapa Area => new(X, Y, Largura, Altura);

    private Secao()
    {
    }

    public static Secao Criar(Guid id, Guid lojaId, Guid grupoId, string nome, string? nomeLadoB, bool dividida, AreaDoMapa area)
    {
        return new Secao { Id = id, LojaId = lojaId, GrupoId = grupoId, Nome = nome, NomeLadoB = dividida ? nomeLadoB : null, Dividida = dividida, X = area.X, Y = area.Y, Largura = area.Largura, Altura = area.Altura };
    }

    public string NomeDoLado(LadoDaSecaoEnum? lado) => lado == LadoDaSecaoEnum.B ? NomeLadoB ?? Nome : Nome;

    /// <summary>Dito ao usuário na rota: "lado de baixo", "lado esquerdo"... Vazio em seção de um lado só.</summary>
    public string? TextoDoLado(LadoDaSecaoEnum? lado)
    {
        if (lado is null || !Dividida) return null;
        var horizontal = Largura >= Altura;
        return lado == LadoDaSecaoEnum.A ? (horizontal ? "lado de cima" : "lado esquerdo") : (horizontal ? "lado de baixo" : "lado direito");
    }
}

/// <summary>
/// Categoria vendida num lado de uma seção. Uma categoria pode aparecer em vários lugares do mesmo mercado
/// (Mercearia em várias gôndolas); a rota vai ao mais próximo. Por isso NÃO existe unicidade por loja+categoria.
/// </summary>
public sealed class SecaoCategoria
{
    public Guid Id { get; private set; }
    public Guid SecaoId { get; private set; }
    public Guid LojaId { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid CategoriaId { get; private set; }
    public LadoDaSecaoEnum Lado { get; private set; }

    private SecaoCategoria()
    {
    }

    public static SecaoCategoria Criar(Guid id, Guid secaoId, Guid lojaId, Guid grupoId, Guid categoriaId, LadoDaSecaoEnum lado)
    {
        return new SecaoCategoria { Id = id, SecaoId = secaoId, LojaId = lojaId, GrupoId = grupoId, CategoriaId = categoriaId, Lado = lado };
    }
}
