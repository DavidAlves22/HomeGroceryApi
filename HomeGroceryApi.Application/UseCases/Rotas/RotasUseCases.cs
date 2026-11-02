using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Listas;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Rotas;

/// <summary>
/// Rota da compra dentro de UM mercado. Só entram os itens que ainda faltam e que não foram marcados como
/// "Não tem aqui" neste mercado: o que já foi comprado em outro mercado nunca aparece. Quem não tem seção
/// para a categoria neste mapa vai para "fora da rota" e continua pendente para o próximo mercado.
/// </summary>
public sealed class PlanejarRotaDaLista : CasoDeUsoDeLista
{
    private readonly ILojaRepository _lojas;
    private readonly ICategoriaRepository _categorias;

    public PlanejarRotaDaLista(IListaRepository listas, ILojaRepository lojas, ICategoriaRepository categorias, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _lojas = lojas;
        _categorias = categorias;
    }

    public async Task<RotaResposta> ExecutarAsync(Guid listaId, RotaRequisicao requisicao, CancellationToken ct)
    {
        var grupoId = Usuario.GrupoId;
        await ObterListaAsync(listaId, ct);

        var loja = await _lojas.ObterAsync(grupoId, requisicao.LojaId, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Mercado não encontrado.");

        var secoes = await _lojas.ListarSecoesAsync(grupoId, loja.Id, ct);
        var vinculos = await _lojas.ListarVinculosAsync(grupoId, loja.Id, ct);
        var itens = await Listas.ListarItensAsync(grupoId, listaId, ct);
        var ordemDaCategoria = (await _categorias.ListarAsync(grupoId, true, ct)).ToDictionary(c => c.Id, c => c.Ordem);

        var pendentes = itens.Where(i => !i.Comprado && !i.LojasSemOItem.Contains(loja.Id)).ToList();
        var demandas = pendentes.Select(i => new DemandaDeRota(i.Id, i.CategoriaId, i.CategoriaId is { } c && ordemDaCategoria.TryGetValue(c, out var ordem) ? ordem : int.MaxValue)).ToList();
        var inicio = requisicao.Posicao is { } p ? new PontoDoMapa(p.X, p.Y) : loja.Entrada;

        var plano = PlanejadorDeRota.Planejar(loja, secoes, vinculos, demandas, inicio, true);
        var seguindoALista = PlanejadorDeRota.Planejar(loja, secoes, vinculos, demandas, inicio, false);

        var itensPorId = itens.ToDictionary(i => i.Id);
        var secoesPorId = secoes.ToDictionary(s => s.Id);
        var paradas = plano.Paradas
            .Select(parada =>
            {
                var secao = secoesPorId[parada.SecaoId];
                return new ParadaResposta(secao.Id, secao.NomeDoLado(parada.Lado), parada.Lado, secao.TextoDoLado(parada.Lado), parada.Posicao.ParaDto(), Metros(parada.Distancia), parada.ItensIds.Select(id => itensPorId[id].ParaResposta()).ToList());
            })
            .ToList();

        var fora = plano.ItensSemLocal.Select(id => new ItemForaDaRotaResposta(itensPorId[id].ParaResposta(), "SemSecao"))
            .Concat(itens.Where(i => !i.Comprado && i.LojasSemOItem.Contains(loja.Id)).Select(i => new ItemForaDaRotaResposta(i.ParaResposta(), "NaoTemNaLoja")))
            .ToList();

        var distancia = Metros(plano.Distancia);
        var tempo = Math.Max(1, (int)Math.Round(distancia / 50.0 + paradas.Sum(x => x.Itens.Count) * 0.4));
        return new RotaResposta(
            loja.Id, inicio.ParaDto(), paradas, plano.Trilha.Select(t => t.ParaDto()).ToList(), distancia, Math.Max(0, Metros(seguindoALista.Distancia) - distancia), tempo, fora,
            itens.Count(i => i.Comprado && i.CompradoNaLojaId != loja.Id));
    }

    private static int Metros(double celulas) => (int)Math.Round(celulas * PlanejadorDeRota.MetrosPorCelula);
}
