using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Application.UseCases.Lojas;

public sealed class ListarLojas
{
    private readonly ILojaRepository _lojas;
    private readonly IUsuarioAtual _usuario;

    public ListarLojas(ILojaRepository lojas, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<LojaResumoResposta>> ExecutarAsync(CancellationToken ct)
    {
        var lojas = await _lojas.ListarAsync(_usuario.GrupoId, false, ct);
        var secoes = await _lojas.ContarSecoesAsync(_usuario.GrupoId, ct);
        return lojas.Select(l => l.ParaResumo(secoes.GetValueOrDefault(l.Id))).ToList();
    }
}

public sealed class ObterLoja
{
    private readonly ILojaRepository _lojas;
    private readonly IUsuarioAtual _usuario;

    public ObterLoja(ILojaRepository lojas, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _usuario = usuario;
    }

    public async Task<LojaResposta> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var loja = await _lojas.ObterAsync(_usuario.GrupoId, id, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Mercado não encontrado.");

        return loja.ParaResposta(await _lojas.ListarSecoesAsync(_usuario.GrupoId, id, ct), await _lojas.ListarVinculosAsync(_usuario.GrupoId, id, ct));
    }
}

/// <summary>Cria um mercado em branco ou copiando o layout de outro (ponto de partida para mercados parecidos).</summary>
public sealed class CriarLoja
{
    private const int LarguraPadrao = 34;
    private const int AlturaPadrao = 40;

    private readonly ILojaRepository _lojas;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public CriarLoja(ILojaRepository lojas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<LojaResposta> ExecutarAsync(CriarLojaRequisicao requisicao, CancellationToken ct)
    {
        var grupoId = _usuario.GrupoId;
        var nome = Entrada.Nome(requisicao.Nome, "O nome do mercado");
        var existentes = await _lojas.ListarAsync(grupoId, false, ct);
        if (existentes.Any(l => string.Equals(l.Nome, nome, StringComparison.OrdinalIgnoreCase))) throw new ErroDeConflito("Já existe um mercado com esse nome.");

        var secoes = new List<Secao>();
        var vinculos = new List<SecaoCategoria>();
        var locais = new List<LocalDoProduto>();
        Loja loja;

        if (requisicao.CopiarDeLojaId is { } origemId)
        {
            var origem = existentes.FirstOrDefault(l => l.Id == origemId) ?? throw new ErroDeNaoEncontrado("Mercado de origem não encontrado.");
            loja = Loja.Criar(Guid.CreateVersion7(), grupoId, nome, origem.Largura, origem.Altura, origem.Entrada, origem.Caixas);

            var novasIds = new Dictionary<Guid, Guid>();
            foreach (var s in await _lojas.ListarSecoesAsync(grupoId, origemId, ct))
            {
                var nova = Secao.Criar(Guid.CreateVersion7(), loja.Id, grupoId, s.Nome, s.NomeLadoB, s.Dividida, s.Area);
                novasIds[s.Id] = nova.Id;
                secoes.Add(nova);
            }

            foreach (var v in await _lojas.ListarVinculosAsync(grupoId, origemId, ct)) vinculos.Add(SecaoCategoria.Criar(Guid.CreateVersion7(), novasIds[v.SecaoId], loja.Id, grupoId, v.CategoriaId, v.Lado));
            foreach (var l in await _lojas.ListarLocaisDeProdutosAsync(grupoId, origemId, ct)) locais.Add(LocalDoProduto.Criar(Guid.CreateVersion7(), grupoId, loja.Id, l.Chave, novasIds[l.SecaoId], l.Lado));
        }
        else
        {
            loja = Loja.Criar(Guid.CreateVersion7(), grupoId, nome, LarguraPadrao, AlturaPadrao, new PontoDoMapa(16, 37), new AreaDoMapa(24, 34, 7, 2));
        }

        await _lojas.AdicionarAsync(loja, ct);
        await _lojas.AdicionarMapaAsync(secoes, vinculos, ct);
        await _lojas.AdicionarLocaisAsync(locais, ct);
        await _unidade.SalvarAsync(ct);
        return loja.ParaResposta(secoes, vinculos);
    }
}

/// <summary>
/// Salva o mapa inteiro de uma vez (o editor gráfico mexe em muitas seções por sessão). As seções antigas
/// saem e as novas entram com ids novos. A versão impede que duas pessoas editando o mesmo mapa se sobrescrevam.
/// </summary>
public sealed class SalvarMapaDaLoja
{
    private readonly ILojaRepository _lojas;
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public SalvarMapaDaLoja(ILojaRepository lojas, ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<LojaResposta> ExecutarAsync(Guid id, SalvarMapaRequisicao requisicao, CancellationToken ct)
    {
        var grupoId = _usuario.GrupoId;
        var loja = await _lojas.ObterAsync(grupoId, id, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Mercado não encontrado.");
        if (loja.Versao != requisicao.Versao) throw new ErroDeConflito("O mapa deste mercado foi alterado por outra pessoa. Atualize e tente de novo.");

        var nome = Entrada.Nome(requisicao.Nome, "O nome do mercado");
        var existentes = await _lojas.ListarAsync(grupoId, false, ct);
        if (existentes.Any(l => l.Id != id && string.Equals(l.Nome, nome, StringComparison.OrdinalIgnoreCase))) throw new ErroDeConflito("Já existe um mercado com esse nome.");

        var categoriasValidas = (await _categorias.ListarAsync(grupoId, false, ct)).Select(c => c.Id).ToHashSet();
        ValidadorDoMapa.Validar(requisicao, categoriasValidas);

        var secoes = new List<Secao>();
        var vinculos = new List<SecaoCategoria>();
        foreach (var pedido in requisicao.Secoes)
        {
            var secao = Secao.Criar(Guid.CreateVersion7(), id, grupoId, pedido.Nome.Trim(), pedido.NomeLadoB?.Trim(), pedido.Dividida, pedido.Area.ParaArea());
            secoes.Add(secao);
            foreach (var categoriaId in pedido.CategoriasLadoA ?? []) vinculos.Add(SecaoCategoria.Criar(Guid.CreateVersion7(), secao.Id, id, grupoId, categoriaId, LadoDaSecaoEnum.A));
            foreach (var categoriaId in pedido.CategoriasLadoB ?? []) vinculos.Add(SecaoCategoria.Criar(Guid.CreateVersion7(), secao.Id, id, grupoId, categoriaId, LadoDaSecaoEnum.B));
        }

        var antigas = await _lojas.ListarSecoesAsync(grupoId, id, ct);

        // As seções saem e voltam com ids novas; o local que o usuário marcou para um produto acompanha a seção
        // que continua no mapa (o app manda o id anterior). Se a seção sumiu ou perdeu o lado B, o local sai.
        var novaPorAntiga = new Dictionary<Guid, Secao>();
        for (var i = 0; i < requisicao.Secoes.Count; i++)
        {
            if (requisicao.Secoes[i].IdAnterior is { } anterior && antigas.Any(a => a.Id == anterior)) novaPorAntiga.TryAdd(anterior, secoes[i]);
        }

        var locais = await _lojas.ListarLocaisDeProdutosAsync(grupoId, id, ct);
        var descartados = new List<LocalDoProduto>();
        foreach (var local in locais)
        {
            if (novaPorAntiga.TryGetValue(local.SecaoId, out var nova) && (nova.Dividida || local.Lado == LadoDaSecaoEnum.A)) local.MoverParaSecao(nova.Id);
            else descartados.Add(local);
        }

        _lojas.RemoverLocais(descartados);
        _lojas.RemoverMapa(antigas, await _lojas.ListarVinculosAsync(grupoId, id, ct));
        loja.DefinirMapa(nome, requisicao.Largura, requisicao.Altura, new PontoDoMapa(requisicao.Entrada.X, requisicao.Entrada.Y), requisicao.Caixas.ParaArea());
        await _lojas.AdicionarMapaAsync(secoes, vinculos, ct);
        await _unidade.SalvarAsync(ct);
        return loja.ParaResposta(secoes, vinculos);
    }
}

/// <summary>Exclusão lógica: itens de listas antigas continuam sabendo em qual mercado foram comprados.</summary>
public sealed class ExcluirLoja
{
    private readonly ILojaRepository _lojas;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public ExcluirLoja(ILojaRepository lojas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _lojas = lojas;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var loja = await _lojas.ObterAsync(_usuario.GrupoId, id, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Mercado não encontrado.");

        loja.Excluir(_relogio.AgoraUtc);
        await _unidade.SalvarAsync(ct);
    }
}
