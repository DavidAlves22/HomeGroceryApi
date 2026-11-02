using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Item da lista padrão (o "modelo" reutilizável). Ao criar uma lista mensal ele é COPIADO:
/// depois disso, mudar ou apagar o item do modelo não toca em nenhuma lista já criada.
/// </summary>
public sealed class ItemDoModelo
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid CategoriaId { get; private set; }
    public UnidadeDeMedidaEnum Unidade { get; private set; }
    public decimal QuantidadePadrao { get; private set; }
    public int Ordem { get; private set; }

    private ItemDoModelo()
    {
    }

    public static ItemDoModelo Criar(Guid id, Guid grupoId, Guid produtoId, string nome, Guid categoriaId, UnidadeDeMedidaEnum unidade, decimal quantidade, int ordem)
    {
        return new ItemDoModelo { Id = id, GrupoId = grupoId, ProdutoId = produtoId, Nome = nome, CategoriaId = categoriaId, Unidade = unidade, QuantidadePadrao = quantidade, Ordem = ordem };
    }

    public void Editar(Guid produtoId, string nome, Guid categoriaId, UnidadeDeMedidaEnum unidade, decimal quantidade)
    {
        ProdutoId = produtoId;
        Nome = nome;
        CategoriaId = categoriaId;
        Unidade = unidade;
        QuantidadePadrao = quantidade;
    }

    public void MoverParaCategoria(Guid categoriaId)
    {
        CategoriaId = categoriaId;
    }

    public void DefinirOrdem(int ordem)
    {
        Ordem = ordem;
    }
}
