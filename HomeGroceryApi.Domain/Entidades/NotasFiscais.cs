namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// "Este código do mercado é este produto." Cada mercado numera seus produtos (o "Código: AR075346" da nota),
/// então a chave é grupo + CNPJ + código. Quem importa a primeira nota escolhe os pares; nas próximas o app já sabe.
/// </summary>
public sealed class VinculoDeProdutoDaNota
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Cnpj { get; private set; } = string.Empty;
    public string Codigo { get; private set; } = string.Empty;
    public Guid ProdutoId { get; private set; }

    private VinculoDeProdutoDaNota()
    {
    }

    public static VinculoDeProdutoDaNota Criar(Guid id, Guid grupoId, string cnpj, string codigo, Guid produtoId)
    {
        return new VinculoDeProdutoDaNota { Id = id, GrupoId = grupoId, Cnpj = cnpj, Codigo = codigo, ProdutoId = produtoId };
    }

    public void ApontarPara(Guid produtoId)
    {
        ProdutoId = produtoId;
    }
}

/// <summary>Registro de uma nota já importada (pela "impressão digital" dos itens), para não contar a mesma compra duas vezes.</summary>
public sealed class NotaImportada
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid ListaId { get; private set; }
    public string Cnpj { get; private set; } = string.Empty;
    public string Impressao { get; private set; } = string.Empty;

    /// <summary>Chave de acesso (44 dígitos) quando a nota veio de um QR; reconhece a mesma nota antes mesmo de colar o texto.</summary>
    public string? ChaveDeAcesso { get; private set; }

    public decimal Total { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTime ImportadaEm { get; private set; }

    private NotaImportada()
    {
    }

    public static NotaImportada Criar(Guid id, Guid grupoId, Guid listaId, string cnpj, string impressao, decimal total, Guid usuarioId, DateTime agora, string? chaveDeAcesso = null)
    {
        return new NotaImportada { Id = id, GrupoId = grupoId, ListaId = listaId, Cnpj = cnpj, Impressao = impressao, ChaveDeAcesso = chaveDeAcesso, Total = total, UsuarioId = usuarioId, ImportadaEm = agora };
    }
}
