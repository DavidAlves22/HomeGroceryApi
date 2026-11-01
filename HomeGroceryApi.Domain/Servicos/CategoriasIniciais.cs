using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Servicos;

/// <summary>
/// Categorias pré-configuradas que todo grupo recebe ao ser criado.
/// </summary>
public static class CategoriasIniciais
{
    public static readonly IReadOnlyList<string> Nomes =
    [
        "Mercearia", "Açougue", "Hortifruti", "Padaria", "Laticínios", "Bebidas",
        "Limpeza", "Higiene pessoal", "Congelados", "Pet", "Outros",
    ];

    public static IReadOnlyList<Categoria> CriarPara(Guid grupoId, Func<Guid> novoId)
    {
        return Nomes.Select((nome, indice) => Categoria.Criar(novoId(), grupoId, nome, indice, true)).ToList();
    }
}
