using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Listas;

/// <summary>
/// Base com o que quase todo caso de uso de lista precisa: achar a lista do grupo atual ou responder 404.
/// Buscar sempre por (grupo, id) é o que impede um usuário de abrir a lista de outro grupo.
/// </summary>
public abstract class CasoDeUsoDeLista
{
    protected CasoDeUsoDeLista(IListaRepository listas, IUsuarioAtual usuario)
    {
        Listas = listas;
        Usuario = usuario;
    }

    protected IListaRepository Listas { get; }
    protected IUsuarioAtual Usuario { get; }

    protected async Task<ListaDeCompras> ObterListaAsync(Guid listaId, CancellationToken ct)
    {
        return await Listas.ObterAsync(Usuario.GrupoId, listaId, ct) ?? throw new ErroDeNaoEncontrado("Lista não encontrada.");
    }

    protected async Task<ItemDaLista> ObterItemAsync(Guid listaId, Guid itemId, CancellationToken ct)
    {
        return await Listas.ObterItemAsync(Usuario.GrupoId, listaId, itemId, ct) ?? throw new ErroDeNaoEncontrado("Item não encontrado.");
    }
}

public sealed class ListarListas
{
    private readonly IListaRepository _listas;
    private readonly IUsuarioAtual _usuario;

    public ListarListas(IListaRepository listas, IUsuarioAtual usuario)
    {
        _listas = listas;
        _usuario = usuario;
    }

    /// <summary>Do mês mais recente para o mais antigo: serve ao Início e ao Histórico.</summary>
    public async Task<IReadOnlyList<ListaResumoResposta>> ExecutarAsync(CancellationToken ct)
    {
        var listas = await _listas.ListarAsync(_usuario.GrupoId, ct);
        var itensPorLista = (await _listas.ListarItensComCompetenciaAsync(_usuario.GrupoId, ct)).GroupBy(i => i.Item.ListaId).ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());

        return listas
            .Select(l => l.ParaResumo(itensPorLista.GetValueOrDefault(l.Id, [])))
            .OrderByDescending(r => r.Competencia, StringComparer.Ordinal)
            .ThenByDescending(r => r.Principal)
            .Select(r => r.ParaResposta())
            .ToList();
    }
}

public sealed class ObterLista : CasoDeUsoDeLista
{
    public ObterLista(IListaRepository listas, IUsuarioAtual usuario) : base(listas, usuario)
    {
    }

    public async Task<ListaResposta> ExecutarAsync(Guid listaId, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        var itens = await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct);
        return lista.ParaListaResposta(itens);
    }
}

/// <summary>
/// Cria a lista do mês. Com origem "Padrao", cada item do modelo é COPIADO para um registro novo
/// (id novo + snapshot de nome, categoria e unidade). Depois disso a lista não tem mais ligação
/// com o modelo: alterar ou apagar a lista padrão nunca muda esta lista.
/// </summary>
public sealed class CriarLista
{
    private readonly IListaRepository _listas;
    private readonly IModeloRepository _modelo;
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public CriarLista(IListaRepository listas, IModeloRepository modelo, ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _listas = listas;
        _modelo = modelo;
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task<ListaResposta> ExecutarAsync(CriarListaRequisicao requisicao, CancellationToken ct)
    {
        var grupoId = _usuario.GrupoId;
        var competencia = Competencia.Parse(requisicao.Competencia);
        var nomeInformado = string.IsNullOrWhiteSpace(requisicao.Nome) ? null : Entrada.Nome(requisicao.Nome, "O nome da lista");
        if (requisicao.Origem != OrigemDaListaEnum.Padrao && requisicao.ItensDoModeloIds is { Count: > 0 }) throw new ErroDeValidacao("Uma lista criada do zero não leva itens da lista padrão.");

        var existePrincipal = await _listas.ExistePrincipalAsync(grupoId, competencia.Chave, ct);
        var (principal, nome) = RegrasDeLista.DefinirPrincipalENome(competencia, nomeInformado, existePrincipal, requisicao.PermitirDuplicada);

        var lista = ListaDeCompras.Criar(Guid.CreateVersion7(), grupoId, competencia.Chave, nome, principal, _usuario.UsuarioId, _relogio.AgoraUtc);
        var itens = requisicao.Origem switch
        {
            OrigemDaListaEnum.Padrao => await CopiarDoModeloAsync(lista, requisicao.ItensDoModeloIds, ct),
            OrigemDaListaEnum.Copiar => await CopiarDeOutraListaAsync(lista, requisicao, ct),
            _ => [],
        };

        await _listas.AdicionarAsync(lista, itens, ct);
        await _unidade.SalvarAsync(ct);
        return lista.ParaListaResposta(itens);
    }

    /// <summary>
    /// Copia os produtos de qualquer lista anterior do grupo. Cada item vira um registro NOVO e começa
    /// pendente: sem marcação, sem quantidade comprada e sem preço pago. O preço que foi pago lá fica só
    /// como REFERÊNCIA para consulta e nunca entra no total gasto do novo mês.
    /// </summary>
    private async Task<List<ItemDaLista>> CopiarDeOutraListaAsync(ListaDeCompras nova, CriarListaRequisicao requisicao, CancellationToken ct)
    {
        if (requisicao.ListaDeOrigemId is not { } origemId) throw new ErroDeValidacao("Escolha a lista que será copiada.");

        var origem = await _listas.ObterAsync(nova.GrupoId, origemId, ct) ?? throw new ErroDeNaoEncontrado("Lista de origem não encontrada.");
        var itensDaOrigem = await _listas.ListarItensAsync(nova.GrupoId, origem.Id, ct);
        var escolhidos = requisicao.ItensDeOrigemIds is null ? itensDaOrigem : itensDaOrigem.Where(i => requisicao.ItensDeOrigemIds.Contains(i.Id)).ToList();

        return escolhidos
            .OrderBy(i => i.Ordem)
            .Select((i, posicao) => ItemDaLista.Criar(
                Guid.CreateVersion7(), nova.Id, nova.GrupoId, i.ProdutoId, i.NomeSnapshot, i.CategoriaId, i.CategoriaNomeSnapshot, i.UnidadeSnapshot,
                i.QuantidadePlanejada, posicao, _usuario.UsuarioId, i.PrecoEstimado, i.PrecoUnitario ?? i.PrecoReferencia))
            .ToList();
    }

    private async Task<List<ItemDaLista>> CopiarDoModeloAsync(ListaDeCompras lista, IReadOnlyList<Guid>? idsSelecionados, CancellationToken ct)
    {
        var modelo = await _modelo.ListarAsync(lista.GrupoId, null, ct);
        var categorias = (await _categorias.ListarAsync(lista.GrupoId, true, ct)).ToDictionary(c => c.Id);

        // Sem seleção explícita = todos os itens do modelo. Com seleção, só os marcados pelo usuário.
        var escolhidos = idsSelecionados is null ? modelo : modelo.Where(i => idsSelecionados.Contains(i.Id)).ToList();

        return escolhidos
            .OrderBy(i => i.Ordem)
            .Select((i, posicao) => ItemDaLista.Criar(
                Guid.CreateVersion7(), lista.Id, lista.GrupoId, i.ProdutoId, i.Nome, i.CategoriaId,
                categorias.TryGetValue(i.CategoriaId, out var c) ? c.Nome : "Outros", i.Unidade, i.QuantidadePadrao, posicao, _usuario.UsuarioId))
            .ToList();
    }
}

public sealed class AdicionarItemNaLista : CasoDeUsoDeLista
{
    private readonly CatalogoDeProdutos _catalogo;
    private readonly IUnidadeDeTrabalho _unidade;

    public AdicionarItemNaLista(IListaRepository listas, CatalogoDeProdutos catalogo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _catalogo = catalogo;
        _unidade = unidade;
    }

    public async Task<ItemDaListaResposta> ExecutarAsync(Guid listaId, ItemDaListaRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome);
        var quantidade = Entrada.Quantidade(requisicao.Quantidade);
        var precoEstimado = Entrada.Preco(requisicao.PrecoEstimado, "O preço estimado");
        var lista = await ObterListaAsync(listaId, ct);
        lista.GarantirEditavel();

        // Produto novo entra só no cadastro central; a lista padrão não é tocada (RN-6).
        var (produto, categoria) = await _catalogo.ResolverAsync(Usuario.GrupoId, nome, requisicao.CategoriaId, requisicao.Unidade, ct);
        var ordem = await Listas.ProximaOrdemDeItemAsync(listaId, ct);
        var item = ItemDaLista.Criar(Guid.CreateVersion7(), listaId, lista.GrupoId, produto.Id, nome, categoria.Id, categoria.Nome, requisicao.Unidade, quantidade, ordem, Usuario.UsuarioId, precoEstimado);

        await Listas.AdicionarItemAsync(item, ct);
        await _unidade.SalvarAsync(ct);
        return item.ParaResposta();
    }
}

public sealed class EditarItemDaLista : CasoDeUsoDeLista
{
    private readonly CatalogoDeProdutos _catalogo;
    private readonly IUnidadeDeTrabalho _unidade;

    public EditarItemDaLista(IListaRepository listas, CatalogoDeProdutos catalogo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _catalogo = catalogo;
        _unidade = unidade;
    }

    public async Task<ItemDaListaResposta> ExecutarAsync(Guid listaId, Guid itemId, EditarItemDaListaRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome);
        var quantidade = Entrada.Quantidade(requisicao.Quantidade);
        var quantidadeComprada = Entrada.QuantidadeOpcional(requisicao.QuantidadeComprada);
        var precoEstimado = Entrada.Preco(requisicao.PrecoEstimado, "O preço estimado");
        var precoPago = Entrada.Preco(requisicao.PrecoUnitario, "O preço pago");
        var lista = await ObterListaAsync(listaId, ct);
        lista.GarantirEditavel();
        var item = await ObterItemAsync(listaId, itemId, ct);

        // Concorrência: quem editou com uma versão antiga não pode sobrescrever a alteração de outra pessoa.
        if (item.Versao != requisicao.Versao) throw new ErroDeConflito("Este item foi alterado por outra pessoa. Atualize a lista e tente de novo.");

        var (produto, categoria) = await _catalogo.ResolverAsync(Usuario.GrupoId, nome, requisicao.CategoriaId, requisicao.Unidade, ct);
        item.Editar(produto.Id, nome, categoria.Id, categoria.Nome, requisicao.Unidade, quantidade, quantidadeComprada, precoEstimado, precoPago);

        await _unidade.SalvarAsync(ct);
        return item.ParaResposta();
    }
}

public sealed class RemoverItemDaLista : CasoDeUsoDeLista
{
    private readonly IUnidadeDeTrabalho _unidade;

    public RemoverItemDaLista(IListaRepository listas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _unidade = unidade;
    }

    /// <summary>Remove só da lista do mês: o cadastro e a lista padrão continuam como estavam (RN-7).</summary>
    public async Task ExecutarAsync(Guid listaId, Guid itemId, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.GarantirEditavel();
        Listas.RemoverItem(await ObterItemAsync(listaId, itemId, ct));
        await _unidade.SalvarAsync(ct);
    }
}

public sealed class MarcarItemComprado : CasoDeUsoDeLista
{
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public MarcarItemComprado(IListaRepository listas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio) : base(listas, usuario)
    {
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<ItemDaListaResposta> ExecutarAsync(Guid listaId, Guid itemId, MarcarCompradoRequisicao requisicao, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.GarantirEditavel();
        var item = await ObterItemAsync(listaId, itemId, ct);

        item.DefinirComprado(requisicao.Comprado, Usuario.UsuarioId, _relogio.AgoraUtc);
        await _unidade.SalvarAsync(ct);
        return item.ParaResposta();
    }
}

public sealed class FinalizarLista : CasoDeUsoDeLista
{
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public FinalizarLista(IListaRepository listas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio) : base(listas, usuario)
    {
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<ListaResumoResposta> ExecutarAsync(Guid listaId, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        var itens = await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct);

        // O total gasto é congelado agora: a lista finalizada guarda o valor real da compra.
        lista.Finalizar(_relogio.AgoraUtc, itens.Sum(CalculoDeTotais.TotalRealizadoDoItem));
        await _unidade.SalvarAsync(ct);
        return lista.ParaResumo(itens).ParaResposta();
    }
}

public sealed class ReabrirLista : CasoDeUsoDeLista
{
    private readonly IUnidadeDeTrabalho _unidade;

    public ReabrirLista(IListaRepository listas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _unidade = unidade;
    }

    public async Task<ListaResumoResposta> ExecutarAsync(Guid listaId, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.Reabrir();
        await _unidade.SalvarAsync(ct);
        return lista.ParaResumo(await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct)).ParaResposta();
    }
}

/// <summary>
/// Ordem manual dos produtos da lista (por exemplo, na ordem dos corredores do mercado).
/// </summary>
public sealed class ReordenarItensDaLista : CasoDeUsoDeLista
{
    private readonly IUnidadeDeTrabalho _unidade;

    public ReordenarItensDaLista(IListaRepository listas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario) : base(listas, usuario)
    {
        _unidade = unidade;
    }

    public async Task ExecutarAsync(Guid listaId, ReordenarRequisicao requisicao, CancellationToken ct)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.GarantirEditavel();

        var itens = await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct);
        var ids = requisicao.Ids ?? [];
        if (ids.Count != itens.Count || ids.Distinct().Count() != ids.Count || !itens.All(i => ids.Contains(i.Id))) throw new ErroDeValidacao("A nova ordem precisa conter todos os itens da lista, sem repetir.");

        for (var posicao = 0; posicao < ids.Count; posicao++) itens.First(i => i.Id == ids[posicao]).DefinirOrdem(posicao);

        await _unidade.SalvarAsync(ct);
    }
}
