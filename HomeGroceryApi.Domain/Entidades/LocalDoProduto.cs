using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Onde um produto foi achado num mercado específico. Vale mais que a categoria da seção: o óleo em promoção na
/// ponta da gôndola, ou a cerveja fora do corredor de bebidas. O produto é identificado pelo nome normalizado
/// (sem acento e minúsculo), porque o item da lista é um nome livre que se repete de um mês para o outro.
/// Um produto pode ter mais de um lugar no mesmo mercado; a rota vai ao mais próximo.
/// </summary>
public sealed class LocalDoProduto
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid LojaId { get; private set; }
    public string Chave { get; private set; } = string.Empty;
    public Guid SecaoId { get; private set; }
    public LadoDaSecaoEnum Lado { get; private set; }

    private LocalDoProduto()
    {
    }

    public static LocalDoProduto Criar(Guid id, Guid grupoId, Guid lojaId, string chave, Guid secaoId, LadoDaSecaoEnum lado)
    {
        return new LocalDoProduto { Id = id, GrupoId = grupoId, LojaId = lojaId, Chave = chave, SecaoId = secaoId, Lado = lado };
    }

    /// <summary>O mapa é salvo por inteiro e as seções ganham ids novas: o local acompanha a seção que continua existindo.</summary>
    public void MoverParaSecao(Guid secaoId)
    {
        SecaoId = secaoId;
    }
}
