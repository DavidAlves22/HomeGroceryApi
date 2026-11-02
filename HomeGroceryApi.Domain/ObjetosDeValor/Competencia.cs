using System.Text.RegularExpressions;
using HomeGroceryApi.Domain.Excecoes;

namespace HomeGroceryApi.Domain.ObjetosDeValor;

/// <summary>
/// Mês de referência de uma lista, no formato "AAAA-MM" (ex.: "2026-11"). Qualquer mês é válido:
/// o app permite criar listas futuras e consultar meses passados.
/// </summary>
public readonly partial record struct Competencia
{
    private static readonly string[] Meses =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    ];

    public string Chave { get; }

    private Competencia(string chave)
    {
        Chave = chave;
    }

    public static Competencia Parse(string? texto)
    {
        if (texto is null || !Padrao().IsMatch(texto)) throw new ErroDeDominio("Informe o mês no formato AAAA-MM.");
        return new Competencia(texto);
    }

    public int Ano => int.Parse(Chave[..4]);

    public int Mes => int.Parse(Chave[5..]);

    public string Nome => $"{Meses[Mes - 1]} de {Ano}";

    public override string ToString() => Chave;

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex Padrao();
}
