using HomeGroceryApi.Domain.Excecoes;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Servicos;

public static class RegrasDeLista
{
    /// <summary>
    /// Decide se a nova lista é a "principal" do mês e qual o nome dela.
    /// Só pode haver uma principal por mês e grupo; uma segunda exige ação explícita do usuário.
    /// </summary>
    public static (bool Principal, string Nome) DefinirPrincipalENome(Competencia competencia, string? nomeInformado, bool jaExistePrincipal, bool permitirDuplicada)
    {
        if (jaExistePrincipal && !permitirDuplicada)
        {
            throw new ErroDeDominio($"Já existe uma lista principal para {competencia.Nome}. Confirme para criar outra lista no mesmo mês.");
        }

        var nome = string.IsNullOrWhiteSpace(nomeInformado) ? competencia.Nome : nomeInformado.Trim();
        return (!jaExistePrincipal, nome);
    }

    public static int PercentualConcluido(int totalDeItens, int comprados)
    {
        return totalDeItens == 0 ? 0 : (int)Math.Round(100.0 * comprados / totalDeItens, MidpointRounding.AwayFromZero);
    }
}
