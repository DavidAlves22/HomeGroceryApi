using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.Servicos;

/// <summary>
/// Regra de "o que some" quando uma conta acaba (pelo próprio dono ou pelo administrador):
/// - grupo em que a pessoa é o ÚNICO vínculo (o pessoal, ou uma família que todos já deixaram) é apagado inteiro;
/// - grupo com outras pessoas continua: ela só sai, e se era a única administradora o membro mais antigo assume.
/// Os dados de uma família são da família: uma pessoa encerrar a conta não pode apagar a lista dos outros.
/// </summary>
public sealed class ServicoDeExclusaoDeConta
{
    private readonly IGrupoRepository _grupos;
    private readonly IEliminadorDeDados _eliminador;
    private readonly INotificador _notificador;

    public ServicoDeExclusaoDeConta(IGrupoRepository grupos, IEliminadorDeDados eliminador, INotificador notificador)
    {
        _grupos = grupos;
        _eliminador = eliminador;
        _notificador = notificador;
    }

    public async Task ExcluirAsync(Guid usuarioId, CancellationToken ct)
    {
        var apagar = new List<Guid>();
        var sair = new List<SaidaDeGrupo>();

        foreach (var vinculo in await _grupos.ListarVinculosDoUsuarioAsync(usuarioId, ct))
        {
            var membros = await _grupos.ListarMembrosAsync(vinculo.GrupoId, ct);
            var outros = membros.Where(m => m.UsuarioId != usuarioId).ToList();
            if (outros.Count == 0)
            {
                apagar.Add(vinculo.GrupoId);
                continue;
            }

            var precisaDeAdministrador = vinculo.Administrador && !outros.Any(m => m.Administrador);
            sair.Add(new SaidaDeGrupo(vinculo.GrupoId, precisaDeAdministrador ? outros.OrderBy(m => m.EntrouEm).First().UsuarioId : null));
        }

        await _eliminador.ApagarContaAsync(new PlanoDeExclusaoDeConta(usuarioId, apagar, sair), ct);

        // A conta já foi apagada: avisar a família é cortesia (a lista de membros se atualiza), e falhar aqui não desfaz nada.
        foreach (var saida in sair)
        {
            try
            {
                await _notificador.PublicarNoGrupoAsync(saida.GrupoId, new EventoDeAlteracao(EventoDeAlteracao.Membros, PorUsuarioId: usuarioId), ct);
            }
            catch
            {
                // Quem não receber o aviso vê a mudança ao recarregar.
            }
        }
    }
}
