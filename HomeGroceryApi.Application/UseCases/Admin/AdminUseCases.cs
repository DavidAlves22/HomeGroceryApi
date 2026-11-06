using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Enums;
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

/// <summary>Mapas de mercado de todos os grupos, para o administrador decidir o que fica no catálogo.</summary>
public sealed class ListarMapasDoPainel
{
    public const int Limite = 200;

    private readonly IConsultasDeAdmin _consultas;

    public ListarMapasDoPainel(IConsultasDeAdmin consultas)
    {
        _consultas = consultas;
    }

    public Task<AdminMapasResposta> ExecutarAsync(CancellationToken ct) => _consultas.MapasAsync(Limite, ct);
}

/// <summary>O desenho de um mapa (mesmo formato da prévia do catálogo), mesmo que ele ainda não esteja no catálogo.</summary>
public sealed class ObterMapaDoPainel
{
    private readonly ILojaRepository _lojas;
    private readonly ICategoriaRepository _categorias;

    public ObterMapaDoPainel(ILojaRepository lojas, ICategoriaRepository categorias)
    {
        _lojas = lojas;
        _categorias = categorias;
    }

    public async Task<CatalogoMercadoDetalheResposta> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var loja = await _lojas.ObterParaModeracaoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Mapa não encontrado.");
        var secoes = await _lojas.ListarSecoesDoCatalogoAsync(id, ct);
        var vinculos = await _lojas.ListarVinculosDoCatalogoAsync(id, ct);
        var nomes = (await _categorias.ListarAsync(loja.GrupoId, true, ct)).ToDictionary(c => c.Id, c => c.Nome);

        List<string> Lado(Guid secaoId, LadoDaSecaoEnum lado) => vinculos.Where(v => v.SecaoId == secaoId && v.Lado == lado && nomes.ContainsKey(v.CategoriaId)).Select(v => nomes[v.CategoriaId]).ToList();

        var respostas = secoes.Select(s => new CatalogoSecaoResposta(s.Nome, s.NomeLadoB, s.Dividida, s.Area.ParaDto(), Lado(s.Id, LadoDaSecaoEnum.A), Lado(s.Id, LadoDaSecaoEnum.B))).ToList();
        return new CatalogoMercadoDetalheResposta(loja.Id, loja.Nome, loja.Largura, loja.Altura, loja.Entrada.ParaDto(), loja.Caixas.ParaDto(), respostas, await _lojas.ContarCopiasAsync(id, ct), loja.Cidade);
    }
}

/// <summary>
/// Moderação: apaga o mapa de vez (sai do catálogo e do grupo do dono). As cópias que outros grupos já fizeram são
/// independentes e ficam. O grupo do dono é avisado em tempo real para a lista de mercados se atualizar.
/// </summary>
public sealed class ExcluirMapaPeloAdmin
{
    private readonly ILojaRepository _lojas;
    private readonly IEliminadorDeDados _eliminador;
    private readonly INotificador _notificador;

    public ExcluirMapaPeloAdmin(ILojaRepository lojas, IEliminadorDeDados eliminador, INotificador notificador)
    {
        _lojas = lojas;
        _eliminador = eliminador;
        _notificador = notificador;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var loja = await _lojas.ObterParaModeracaoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Mapa não encontrado.");
        await _eliminador.ApagarMapaAsync(id, ct);

        try
        {
            await _notificador.PublicarNoGrupoAsync(loja.GrupoId, new EventoDeAlteracao(EventoDeAlteracao.Lojas), ct);
        }
        catch
        {
            // O mapa já foi apagado: quem não receber o aviso vê a mudança ao recarregar.
        }
    }
}

/// <summary>
/// Encerra a conta de outra pessoa (moderação). Contas de administrador ficam de fora: é a mesma proteção do bloqueio,
/// e impede o dono do app de se apagar sem querer.
/// </summary>
public sealed class ExcluirContaPeloAdmin
{
    private readonly IServicoDeContas _contas;
    private readonly IAdministradores _administradores;
    private readonly ServicoDeExclusaoDeConta _exclusao;

    public ExcluirContaPeloAdmin(IServicoDeContas contas, IAdministradores administradores, ServicoDeExclusaoDeConta exclusao)
    {
        _contas = contas;
        _administradores = administradores;
        _exclusao = exclusao;
    }

    public async Task ExecutarAsync(Guid usuarioId, CancellationToken ct)
    {
        var conta = await _contas.ObterPorIdAsync(usuarioId, ct) ?? throw new ErroDeNaoEncontrado("Conta não encontrada.");
        if (_administradores.EhAdministrador(conta.Email)) throw new ErroDeValidacao("Contas de administrador não podem ser excluídas.");

        await _exclusao.ExcluirAsync(usuarioId, ct);
    }
}
