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
        "Limpeza", "Higiene pessoal", "Congelados", "Pet",

        // Corredores do mapa de mercado (um por gôndola), para a rota ir direto ao lugar certo.
        "Refrigerantes", "Cervejas", "Bebidas alcoólicas", "Águas", "Sucos",
        "Conservas", "Enlatados", "Macarrão", "Temperos", "Condimentos", "Farinhas", "Matinais", "Bolachas", "Chocolates", "Salgadinhos", "Sobremesas",
        "Leites", "Iogurtes", "Ovos", "Frios", "Embutidos", "Rotisseria", "Carnes congeladas",
        "Shampoos", "Absorventes", "Perfumaria", "Papéis", "Utilidades", "Brinquedos", "Limpeza carro",

        "Outros",
    ];

    public static IReadOnlyList<Categoria> CriarPara(Guid grupoId, Func<Guid> novoId)
    {
        return Nomes.Select((nome, indice) => Categoria.Criar(novoId(), grupoId, nome, indice, true)).ToList();
    }
}
