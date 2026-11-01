namespace HomeGroceryApi.Application.Opcoes;

public sealed class JwtOpcoes
{
    public const string Secao = "Jwt";

    public string Emissor { get; set; } = string.Empty;
    public string Audiencia { get; set; } = string.Empty;
    public string ChaveSecreta { get; set; } = string.Empty;
    public int MinutosDeValidadeDoToken { get; set; } = 30;
    public int DiasDeValidadeDoTokenDeRenovacao { get; set; } = 30;
}
