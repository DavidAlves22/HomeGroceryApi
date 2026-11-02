using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Um mercado e o desenho do seu layout numa grade (Largura x Altura células). Cada grupo tem N mercados,
/// cada um com o próprio mapa. A exclusão é lógica porque itens de listas antigas guardam em qual mercado
/// foram comprados. `Versao` evita que duas pessoas editando o mapa ao mesmo tempo se sobrescrevam.
/// </summary>
public sealed class Loja
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int Largura { get; private set; }
    public int Altura { get; private set; }
    public int EntradaX { get; private set; }
    public int EntradaY { get; private set; }
    public int CaixasX { get; private set; }
    public int CaixasY { get; private set; }
    public int CaixasLargura { get; private set; }
    public int CaixasAltura { get; private set; }
    public int Versao { get; private set; }
    public DateTime? ExcluidoEm { get; private set; }

    public PontoDoMapa Entrada => new(EntradaX, EntradaY);

    public AreaDoMapa Caixas => new(CaixasX, CaixasY, CaixasLargura, CaixasAltura);

    private Loja()
    {
    }

    public static Loja Criar(Guid id, Guid grupoId, string nome, int largura, int altura, PontoDoMapa entrada, AreaDoMapa caixas)
    {
        var loja = new Loja { Id = id, GrupoId = grupoId, Versao = 1 };
        loja.Aplicar(nome, largura, altura, entrada, caixas);
        return loja;
    }

    public void DefinirMapa(string nome, int largura, int altura, PontoDoMapa entrada, AreaDoMapa caixas)
    {
        Aplicar(nome, largura, altura, entrada, caixas);
        Versao++;
    }

    public void Excluir(DateTime agora)
    {
        ExcluidoEm ??= agora;
    }

    private void Aplicar(string nome, int largura, int altura, PontoDoMapa entrada, AreaDoMapa caixas)
    {
        Nome = nome;
        Largura = largura;
        Altura = altura;
        EntradaX = entrada.X;
        EntradaY = entrada.Y;
        CaixasX = caixas.X;
        CaixasY = caixas.Y;
        CaixasLargura = caixas.Largura;
        CaixasAltura = caixas.Altura;
    }
}
