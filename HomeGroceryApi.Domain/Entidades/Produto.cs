using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Cadastro central de produtos do grupo, reaproveitado ao montar listas (evita "Arroz" e "arroz" duplicados).
/// Exclusão lógica: itens de listas antigas continuam guardando o próprio snapshot.
/// </summary>
public sealed class Produto
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid CategoriaId { get; private set; }
    public UnidadeDeMedidaEnum UnidadePadrao { get; private set; }
    public string? Descricao { get; private set; }
    public bool Favorito { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? ExcluidoEm { get; private set; }

    private Produto()
    {
    }

    public static Produto Criar(Guid id, Guid grupoId, string nome, Guid categoriaId, UnidadeDeMedidaEnum unidade, DateTime agora)
    {
        return new Produto { Id = id, GrupoId = grupoId, Nome = nome, CategoriaId = categoriaId, UnidadePadrao = unidade, CriadoEm = agora };
    }

    public void Editar(string nome, Guid categoriaId, UnidadeDeMedidaEnum unidade, string? descricao)
    {
        Nome = nome;
        CategoriaId = categoriaId;
        UnidadePadrao = unidade;
        Descricao = descricao;
    }

    public void DefinirFavorito(bool favorito)
    {
        Favorito = favorito;
    }

    public void MoverParaCategoria(Guid categoriaId)
    {
        CategoriaId = categoriaId;
    }

    public void Excluir(DateTime agora)
    {
        ExcluidoEm ??= agora;
    }
}
