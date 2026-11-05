using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Interfaces;

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

public sealed class ConsultarInsightsDoPainel
{
    private readonly IConsultasDeAdmin _consultas;
    private readonly IRelogio _relogio;

    public ConsultarInsightsDoPainel(IConsultasDeAdmin consultas, IRelogio relogio)
    {
        _consultas = consultas;
        _relogio = relogio;
    }

    public Task<AdminInsightsResposta> ExecutarAsync(CancellationToken ct) => _consultas.InsightsAsync(_relogio.AgoraUtc, ct);
}

public sealed class ListarUsuariosDoPainel
{
    public const int Tamanho = 50;

    private readonly IConsultasDeAdmin _consultas;
    private readonly IAdministradores _administradores;

    public ListarUsuariosDoPainel(IConsultasDeAdmin consultas, IAdministradores administradores)
    {
        _consultas = consultas;
        _administradores = administradores;
    }

    public async Task<AdminUsuariosResposta> ExecutarAsync(int pagina, CancellationToken ct)
    {
        var resposta = await _consultas.UsuariosAsync(Math.Max(1, pagina), Tamanho, ct);

        // O papel vem da configuração, não do banco; a tela usa para não oferecer "Bloquear" a um administrador.
        return resposta with { Itens = resposta.Itens.Select(u => u with { EhAdmin = _administradores.EhAdministrador(u.Email) }).ToList() };
    }
}

/// <summary>
/// Bloqueia ou libera uma conta. Bloquear revoga todos os tokens de renovação; o token de acesso que ela ainda tenha deixa
/// de valer na próxima requisição (middleware). Administradores não podem ser bloqueados, o que também impede o dono de
/// se trancar para fora do próprio painel.
/// </summary>
public sealed class DefinirBloqueioDeConta
{
    private readonly IServicoDeContas _contas;
    private readonly IAdministradores _administradores;
    private readonly ITokenDeRenovacaoRepository _sessoes;
    private readonly IRelogio _relogio;

    public DefinirBloqueioDeConta(IServicoDeContas contas, IAdministradores administradores, ITokenDeRenovacaoRepository sessoes, IRelogio relogio)
    {
        _contas = contas;
        _administradores = administradores;
        _sessoes = sessoes;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(Guid usuarioId, bool bloqueado, CancellationToken ct)
    {
        var conta = await _contas.ObterPorIdAsync(usuarioId, ct) ?? throw new ErroDeNaoEncontrado("Conta não encontrada.");
        if (bloqueado && _administradores.EhAdministrador(conta.Email)) throw new ErroDeValidacao("Contas de administrador não podem ser bloqueadas.");

        var agora = _relogio.AgoraUtc;
        await _contas.DefinirBloqueioAsync(usuarioId, bloqueado ? agora : null, ct);
        if (bloqueado) await _sessoes.RevogarTodosDoUsuarioAsync(usuarioId, agora, ct);
    }
}
