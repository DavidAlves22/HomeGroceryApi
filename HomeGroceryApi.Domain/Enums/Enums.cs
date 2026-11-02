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
}
