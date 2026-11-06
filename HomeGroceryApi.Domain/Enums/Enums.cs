namespace HomeGroceryApi.Domain.Enums;

public enum UnidadeDeMedidaEnum
{
    Unidade = 1,
    Kg = 2,
    G = 3,
    Litro = 4,
    Ml = 5,
    Pacote = 6,
    Caixa = 7,
}

public enum StatusDaListaEnum
{
    EmAndamento = 1,
    Finalizada = 2,
}

public enum OrigemDaListaEnum
{
    Zero = 1,
    Padrao = 2,
    Copiar = 3,
    Sugestao = 4,
}

public enum TipoDeMensagemEnum
{
    Feedback = 1,
    Solicitacao = 2,
}

public enum StatusDaMensagemEnum
{
    Recebida = 1,
    EmAnalise = 2,
    Respondida = 3,
    Concluida = 4,
}
