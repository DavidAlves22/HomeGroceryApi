using HomeGroceryApi.Application.Dtos;

namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// Leituras agregadas de TODOS os grupos, só para o painel de administração. É a única porta que olha além do grupo
/// do usuário atual, por isso ela só é usada por casos de uso protegidos pelo papel Admin.
/// </summary>
public interface IConsultasDeAdmin
{
    Task<AdminResumoResposta> ResumoAsync(DateTime agora, CancellationToken ct);

    /// <summary>Contagens por dia (dia de Brasília) dos últimos <paramref name="dias"/> dias, incluindo hoje.</summary>
    Task<AdminSeriesResposta> SeriesAsync(DateTime agora, int dias, CancellationToken ct);

    Task<AdminUsuariosResposta> UsuariosAsync(int pagina, int tamanho, CancellationToken ct);
}
