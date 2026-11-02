using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Categorias;

public sealed class ListarCategorias
{
    private readonly ICategoriaRepository _categorias;
    private readonly IUsuarioAtual _usuario;

    public ListarCategorias(ICategoriaRepository categorias, IUsuarioAtual usuario)
    {
        _categorias = categorias;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<CategoriaResposta>> ExecutarAsync(CancellationToken ct)
    {
        var categorias = await _categorias.ListarAsync(_usuario.GrupoId, false, ct);
        return categorias.Select(c => c.ParaResposta()).ToList();
    }
}

public sealed class CriarCategoria
{
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public CriarCategoria(ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<CategoriaResposta> ExecutarAsync(NomeRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome, "O nome da categoria");
        var existentes = await _categorias.ListarAsync(_usuario.GrupoId, false, ct);
        if (existentes.Any(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase))) throw new ErroDeConflito("Já existe uma categoria com esse nome.");

        var ordem = existentes.Count == 0 ? 0 : existentes.Max(c => c.Ordem) + 1;
        var categoria = Categoria.Criar(Guid.CreateVersion7(), _usuario.GrupoId, nome, ordem, false);
        await _categorias.AdicionarAsync(categoria, ct);
        await _unidade.SalvarAsync(ct);
        return categoria.ParaResposta();
    }
}

public sealed class RenomearCategoria
{
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public RenomearCategoria(ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<CategoriaResposta> ExecutarAsync(Guid id, NomeRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome, "O nome da categoria");
        var categoria = await _categorias.ObterAsync(_usuario.GrupoId, id, ct);
        if (categoria is null || categoria.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Categoria não encontrada.");

        var existentes = await _categorias.ListarAsync(_usuario.GrupoId, false, ct);
        if (existentes.Any(c => c.Id != id && string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase))) throw new ErroDeConflito("Já existe uma categoria com esse nome.");

        // Renomear NÃO altera o nome guardado em itens de listas já criadas: eles têm o próprio snapshot.
        categoria.Renomear(nome);
        await _unidade.SalvarAsync(ct);
        return categoria.ParaResposta();
    }
}

/// <summary>
/// Exclusão lógica. Produtos e itens da lista padrão que usavam a categoria passam para "Outros";
/// itens de listas antigas continuam mostrando o nome que tinham na época.
/// </summary>
public sealed class ExcluirCategoria
{
    private readonly ICategoriaRepository _categorias;
    private readonly IProdutoRepository _produtos;
    private readonly IModeloRepository _modelo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public ExcluirCategoria(ICategoriaRepository categorias, IProdutoRepository produtos, IModeloRepository modelo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _categorias = categorias;
        _produtos = produtos;
        _modelo = modelo;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var grupoId = _usuario.GrupoId;
        var categorias = await _categorias.ListarAsync(grupoId, false, ct);
        var categoria = categorias.FirstOrDefault(c => c.Id == id) ?? throw new ErroDeNaoEncontrado("Categoria não encontrada.");

        var destino = categorias.FirstOrDefault(c => c.Id != id && string.Equals(c.Nome, "Outros", StringComparison.OrdinalIgnoreCase))
            ?? throw new ErroDeConflito("Crie uma categoria chamada \"Outros\" antes de excluir esta, para receber os produtos dela.");

        foreach (var produto in (await _produtos.BuscarAsync(grupoId, null, ct)).Where(p => p.CategoriaId == id)) produto.MoverParaCategoria(destino.Id);
        foreach (var item in (await _modelo.ListarAsync(grupoId, null, ct)).Where(i => i.CategoriaId == id)) item.MoverParaCategoria(destino.Id);

        categoria.Excluir(_relogio.AgoraUtc);
        await _unidade.SalvarAsync(ct);
    }
}

public sealed class ReordenarCategorias
{
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public ReordenarCategorias(ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<CategoriaResposta>> ExecutarAsync(ReordenarRequisicao requisicao, CancellationToken ct)
    {
        var categorias = await _categorias.ListarAsync(_usuario.GrupoId, false, ct);
        var ids = requisicao.Ids ?? [];
        if (ids.Count != categorias.Count || ids.Distinct().Count() != ids.Count || !categorias.All(c => ids.Contains(c.Id))) throw new ErroDeValidacao("A nova ordem precisa conter todas as categorias, sem repetir.");

        for (var posicao = 0; posicao < ids.Count; posicao++) categorias.First(c => c.Id == ids[posicao]).DefinirOrdem(posicao);

        await _unidade.SalvarAsync(ct);
        return categorias.OrderBy(c => c.Ordem).Select(c => c.ParaResposta()).ToList();
    }
}
