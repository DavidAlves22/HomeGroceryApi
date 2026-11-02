namespace HomeGroceryApi.Domain.Excecoes;

/// <summary>
/// Violação de uma regra de negócio dentro do domínio (ex.: editar uma lista já finalizada).
/// A API devolve 422 com a mensagem, que já é escrita para o usuário.
/// </summary>
public sealed class ErroDeDominio : Exception
{
    public ErroDeDominio(string mensagem) : base(mensagem)
    {
    }
}
