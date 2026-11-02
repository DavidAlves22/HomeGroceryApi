namespace HomeGroceryApi.Application.Excecoes;

/// <summary>
/// Erro esperado de negócio. A API converte cada subtipo num status HTTP em um só lugar
/// (middleware), então os casos de uso apenas lançam e não conhecem HTTP.
/// </summary>
public abstract class ErroDeAplicacao : Exception
{
    protected ErroDeAplicacao(string mensagem) : base(mensagem)
    {
    }
}

public sealed class ErroDeValidacao : ErroDeAplicacao
{
    public IReadOnlyList<string> Detalhes { get; }

    public ErroDeValidacao(IReadOnlyList<string> detalhes) : base(detalhes.FirstOrDefault() ?? "Dados inválidos.")
    {
        Detalhes = detalhes;
    }

    public ErroDeValidacao(string detalhe) : this([detalhe])
    {
    }
}

public sealed class ErroDeAutenticacao : ErroDeAplicacao
{
    public ErroDeAutenticacao(string mensagem) : base(mensagem)
    {
    }
}

public sealed class ErroDeConflito : ErroDeAplicacao
{
    public ErroDeConflito(string mensagem) : base(mensagem)
    {
    }
}

public sealed class ErroDeNaoEncontrado : ErroDeAplicacao
{
    public ErroDeNaoEncontrado(string mensagem) : base(mensagem)
    {
    }
}

public sealed class ErroDeAcessoNegado : ErroDeAplicacao
{
    public ErroDeAcessoNegado(string mensagem) : base(mensagem)
    {
    }
}
