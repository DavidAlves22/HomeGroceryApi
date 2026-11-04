using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Admin;

/// <summary>Números gerais do app (usuários, uso, listas). A proteção por papel Admin fica no controller.</summary>
public sealed class ConsultarResumoDoPainel
{
    private readonly IConsultasDeAdmin _consultas;
    private readonly IRelogio _relogio;

    public ConsultarResumoDoPainel(IConsultasDeAdmin consultas, IRelogio relogio)
    {
        _consultas = consultas;
        _relogio = relogio;
    }

    public Task<AdminResumoResposta> ExecutarAsync(CancellationToken ct) => _consultas.ResumoAsync(_relogio.AgoraUtc, ct);
}

public sealed class ConsultarSeriesDoPainel
{
    public const int DiasPadrao = 30;
    public const int DiasMaximo = 90;

    private readonly IConsultasDeAdmin _consultas;
    private readonly IRelogio _relogio;

    public ConsultarSeriesDoPainel(IConsultasDeAdmin consultas, IRelogio relogio)
    {
        _consultas = consultas;
        _relogio = relogio;
    }

    /// <summary>Janela fora do intervalo 7 a 90 dias é ajustada em vez de recusada: o gráfico sempre abre.</summary>
    public Task<AdminSeriesResposta> ExecutarAsync(int dias, CancellationToken ct)
    {
        var janela = dias <= 0 ? DiasPadrao : Math.Clamp(dias, 7, DiasMaximo);
        return _consultas.SeriesAsync(_relogio.AgoraUtc, janela, ct);
    }
}

public sealed class ListarUsuariosDoPainel
{
    public const int Tamanho = 50;

    private readonly IConsultasDeAdmin _consultas;

    public ListarUsuariosDoPainel(IConsultasDeAdmin consultas)
    {
        _consultas = consultas;
    }

    public Task<AdminUsuariosResposta> ExecutarAsync(int pagina, CancellationToken ct) => _consultas.UsuariosAsync(Math.Max(1, pagina), Tamanho, ct);
}
