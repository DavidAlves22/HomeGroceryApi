using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Listas;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
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

        var locaisPorProduto = (await _lojas.ListarLocaisDeProdutosAsync(grupoId, loja.Id, ct)).GroupBy(l => l.Chave).ToDictionary(g => g.Key, g => g.Select(l => new LocalProprio(l.SecaoId, l.Lado)).ToList());

        var pendentes = itens.Where(i => !i.Comprado && !i.LojasSemOItem.Contains(loja.Id)).ToList();
        var demandas = pendentes.Select(i => new DemandaDeRota(i.Id, i.CategoriaId, i.CategoriaId is { } c && ordemDaCategoria.TryGetValue(c, out var ordem) ? ordem : int.MaxValue, locaisPorProduto.GetValueOrDefault(AnaliseDeCompras.Normalizar(i.NomeSnapshot)))).ToList();
        // O app mostra "local que você marcou" e usa isto para trocar ou apagar o local.
        var locaisDosItens = pendentes
            .SelectMany(i => (locaisPorProduto.GetValueOrDefault(AnaliseDeCompras.Normalizar(i.NomeSnapshot)) ?? []).Select(l => new LocalDoItemResposta(i.Id, l.SecaoId, l.Lado)))
            .ToList();
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
            itens.Count(i => i.Comprado && i.CompradoNaLojaId != loja.Id),
            locaisDosItens);
    }

    private static int Metros(double celulas) => (int)Math.Round(celulas * PlanejadorDeRota.MetrosPorCelula);
}

/// <summary>
/// "Achei em outro lugar": grava onde o produto do item fica NESTE mercado. Vale para o produto (pelo nome) e
/// para o grupo todo, então a próxima lista e as outras pessoas já usam o local. Substitui os locais anteriores
/// do produto neste mercado; lista vazia apaga e a rota volta a usar a categoria da seção.
/// </summary>
public sealed class DefinirLocalDoItem : CasoDeUsoDeLista
{
    private const int MaximoDeLocais = 5;

    private readonly ILojaRepository _lojas;
    private readonly IUnidadeDeTrabalho _unidade;

    public DefinirLocalDoItem(IListaRepository listas, ILojaRepository lojas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _lojas = lojas;
        _unidade = unidade;
    }

    public async Task<IReadOnlyList<LocalDoItemResposta>> ExecutarAsync(Guid listaId, Guid itemId, DefinirLocalDoItemRequisicao requisicao, CancellationToken ct)
    {
        var grupoId = Usuario.GrupoId;
        await ObterListaAsync(listaId, ct);
        var item = await ObterItemAsync(listaId, itemId, ct);

        var loja = await _lojas.ObterAsync(grupoId, requisicao.LojaId, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeValidacao("Escolha um mercado válido.");

        var pedidos = (requisicao.Locais ?? []).Distinct().ToList();
        if (pedidos.Count > MaximoDeLocais) throw new ErroDeValidacao($"Um produto pode ter no máximo {MaximoDeLocais} locais por mercado.");

        var secoes = (await _lojas.ListarSecoesAsync(grupoId, loja.Id, ct)).ToDictionary(s => s.Id);
        foreach (var pedido in pedidos)
        {
            if (!secoes.TryGetValue(pedido.SecaoId, out var secao)) throw new ErroDeValidacao("Escolha uma seção deste mercado.");
            if (pedido.Lado == LadoDaSecaoEnum.B && !secao.Dividida) throw new ErroDeValidacao($"A seção \"{secao.Nome}\" tem um lado só.");
        }

        // Compara com o que já existe: só apaga o que saiu e só cria o que entrou (evita apagar e recriar a mesma linha).
        var chave = AnaliseDeCompras.Normalizar(item.NomeSnapshot);
        var atuais = (await _lojas.ListarLocaisDeProdutosAsync(grupoId, loja.Id, ct)).Where(l => l.Chave == chave).ToList();
        _lojas.RemoverLocais(atuais.Where(a => !pedidos.Any(p => p.SecaoId == a.SecaoId && p.Lado == a.Lado)));
        var novos = pedidos.Where(p => !atuais.Any(a => a.SecaoId == p.SecaoId && a.Lado == p.Lado)).Select(p => LocalDoProduto.Criar(Guid.CreateVersion7(), grupoId, loja.Id, chave, p.SecaoId, p.Lado)).ToList();
        await _lojas.AdicionarLocaisAsync(novos, ct);
        await _unidade.SalvarAsync(ct);

        return pedidos.Select(p => new LocalDoItemResposta(item.Id, p.SecaoId, p.Lado)).ToList();
    }
}
