using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Modelo;

public sealed class ListarItensDoModelo
{
    private readonly IModeloRepository _modelo;
    private readonly ICategoriaRepository _categorias;
    private readonly IUsuarioAtual _usuario;

    public ListarItensDoModelo(IModeloRepository modelo, ICategoriaRepository categorias, IUsuarioAtual usuario)
    {
        _modelo = modelo;
        _categorias = categorias;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<ItemDoModeloResposta>> ExecutarAsync(string? termo, CancellationToken ct)
    {
        var itens = await _modelo.ListarAsync(_usuario.GrupoId, termo, ct);
        var categorias = (await _categorias.ListarAsync(_usuario.GrupoId, true, ct)).ToDictionary(c => c.Id);
        return itens.Select(i => i.ParaResposta(categorias)).ToList();
    }
}

public sealed class AdicionarItemAoModelo
{
    private readonly IModeloRepository _modelo;
    private readonly ICategoriaRepository _categorias;
    private readonly CatalogoDeProdutos _catalogo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public AdicionarItemAoModelo(IModeloRepository modelo, ICategoriaRepository categorias, CatalogoDeProdutos catalogo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _modelo = modelo;
        _categorias = categorias;
        _catalogo = catalogo;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<ItemDoModeloResposta> ExecutarAsync(ItemDoModeloRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome);
        var quantidade = Entrada.Quantidade(requisicao.Quantidade);
        var grupoId = _usuario.GrupoId;

        var (produto, categoria) = await _catalogo.ResolverAsync(grupoId, nome, requisicao.CategoriaId, requisicao.Unidade, ct);
        var ordem = await _modelo.ProximaOrdemAsync(grupoId, ct);
        var item = ItemDoModelo.Criar(Guid.CreateVersion7(), grupoId, produto.Id, nome, categoria.Id, requisicao.Unidade, quantidade, ordem);

        await _modelo.AdicionarAsync(item, ct);
        await _unidade.SalvarAsync(ct);
        return item.ParaResposta(new Dictionary<Guid, Categoria> { [categoria.Id] = categoria });
    }
}

public sealed class EditarItemDoModelo
{
    private readonly IModeloRepository _modelo;
    private readonly CatalogoDeProdutos _catalogo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public EditarItemDoModelo(IModeloRepository modelo, CatalogoDeProdutos catalogo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _modelo = modelo;
        _catalogo = catalogo;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<ItemDoModeloResposta> ExecutarAsync(Guid id, ItemDoModeloRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome);
        var quantidade = Entrada.Quantidade(requisicao.Quantidade);
        var item = await _modelo.ObterAsync(_usuario.GrupoId, id, ct) ?? throw new ErroDeNaoEncontrado("Item da lista padrão não encontrado.");

        var (produto, categoria) = await _catalogo.ResolverAsync(_usuario.GrupoId, nome, requisicao.CategoriaId, requisicao.Unidade, ct);

        // Só o modelo muda aqui; as listas mensais já criadas continuam exatamente como estão.
        item.Editar(produto.Id, nome, categoria.Id, requisicao.Unidade, quantidade);
        await _unidade.SalvarAsync(ct);
        return item.ParaResposta(new Dictionary<Guid, Categoria> { [categoria.Id] = categoria });
    }
}

public sealed class RemoverItemDoModelo
{
    private readonly IModeloRepository _modelo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public RemoverItemDoModelo(IModeloRepository modelo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _modelo = modelo;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var item = await _modelo.ObterAsync(_usuario.GrupoId, id, ct) ?? throw new ErroDeNaoEncontrado("Item da lista padrão não encontrado.");
        _modelo.Remover(item);
        await _unidade.SalvarAsync(ct);
    }
}

public sealed class ReordenarItensDoModelo
{
    private readonly IModeloRepository _modelo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public ReordenarItensDoModelo(IModeloRepository modelo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _modelo = modelo;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task ExecutarAsync(ReordenarRequisicao requisicao, CancellationToken ct)
    {
        var itens = await _modelo.ListarAsync(_usuario.GrupoId, null, ct);
        var ids = requisicao.Ids ?? [];
        if (ids.Count != itens.Count || ids.Distinct().Count() != ids.Count || !itens.All(i => ids.Contains(i.Id))) throw new ErroDeValidacao("A nova ordem precisa conter todos os itens, sem repetir.");

        for (var posicao = 0; posicao < ids.Count; posicao++) itens.First(i => i.Id == ids[posicao]).DefinirOrdem(posicao);

        await _unidade.SalvarAsync(ct);
    }
}
