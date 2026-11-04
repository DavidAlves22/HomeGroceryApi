using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Um mercado e o desenho do seu layout numa grade (Largura x Altura células). Cada grupo tem N mercados,
/// cada um com o próprio mapa. A exclusão é lógica porque itens de listas antigas guardam em qual mercado
/// foram comprados. `Versao` evita que duas pessoas editando o mapa ao mesmo tempo se sobrescrevam.
/// </summary>
public sealed class Loja
{
    /// <summary>
    /// Todo mapa que cumpre a regra entra no catálogo para outros grupos copiarem (a cópia é independente; o original nunca é
    /// editado por outro grupo). A regra evita mapas errados ou vazios: ao menos 8 seções COM NOME próprio (o nome padrão do
    /// editor, "Nova seção", não conta) e a cidade preenchida.
    /// </summary>
    public const int MinimoDeSecoesParaOCatalogo = 8;

    /// <summary>Nome que o editor dá a uma seção recém-criada; seção que ainda o tem não conta para o catálogo.</summary>
    public const string NomePadraoDaSecao = "Nova seção";

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

    /// <summary>CNPJ do mercado (só dígitos), aprendido ao importar notas fiscais: a próxima nota dele já abre no mercado certo.</summary>
    public string? Cnpj { get; private set; }

    /// <summary>Cidade do mercado (obrigatória em mercados novos). Mapa sem cidade não aparece no catálogo.</summary>
    public string? Cidade { get; private set; }

    /// <summary>Cidade em minúsculas e sem acento ("Sao Paulo" acha "São Paulo"): é o que o filtro do catálogo compara.</summary>
    public string? CidadeBusca { get; private set; }

    /// <summary>Mapa do catálogo de onde esta cópia nasceu (serve para contar quantas vezes o mapa foi copiado).</summary>
    public Guid? OrigemId { get; private set; }

    public DateTime? ExcluidoEm { get; private set; }

    public PontoDoMapa Entrada => new(EntradaX, EntradaY);

    public AreaDoMapa Caixas => new(CaixasX, CaixasY, CaixasLargura, CaixasAltura);

    private Loja()
    {
    }

    public static Loja Criar(Guid id, Guid grupoId, string nome, int largura, int altura, PontoDoMapa entrada, AreaDoMapa caixas, Guid? origemId = null, string? cidade = null)
    {
        var loja = new Loja { Id = id, GrupoId = grupoId, Versao = 1, OrigemId = origemId };
        loja.Aplicar(nome, largura, altura, entrada, caixas);
        if (cidade is not null) loja.DefinirCidade(cidade);
        return loja;
    }

    public void DefinirMapa(string nome, int largura, int altura, PontoDoMapa entrada, AreaDoMapa caixas)
    {
        Aplicar(nome, largura, altura, entrada, caixas);
        Versao++;
    }

    /// <summary>A validação do texto (tamanho, obrigatoriedade) é do caso de uso; aqui só guarda as duas formas.</summary>
    public void DefinirCidade(string cidade)
    {
        Cidade = cidade;
        CidadeBusca = AnaliseDeCompras.Normalizar(cidade);
    }

    /// <summary>Não mexe na versão do mapa: guardar o CNPJ não é editar o desenho.</summary>
    public void DefinirCnpj(string? cnpj)
    {
        Cnpj = cnpj;
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
