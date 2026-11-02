using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Produtos;

/// <summary>
/// Busca no cadastro central. O app usa isto ao digitar um nome, para sugerir produtos que já existem.
/// </summary>
public sealed class BuscarProdutos
{
    private readonly IProdutoRepository _produtos;
    private readonly IUsuarioAtual _usuario;

    public BuscarProdutos(IProdutoRepository produtos, IUsuarioAtual usuario)
    {
        _produtos = produtos;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<ProdutoResposta>> ExecutarAsync(string? termo, CancellationToken ct)
    {
        var produtos = await _produtos.BuscarAsync(_usuario.GrupoId, termo, ct);
        return produtos.Select(p => p.ParaResposta()).ToList();
    }
}

public sealed class EditarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public EditarProduto(IProdutoRepository produtos, ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _produtos = produtos;
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<ProdutoResposta> ExecutarAsync(Guid id, EditarProdutoRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome);
        var produto = await _produtos.ObterAsync(_usuario.GrupoId, id, ct);
        if (produto is null || produto.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Produto não encontrado.");

        var categoria = await _categorias.ObterAsync(_usuario.GrupoId, requisicao.CategoriaId, ct);
        if (categoria is null || categoria.ExcluidoEm is not null) throw new ErroDeValidacao("Escolha uma categoria válida.");

        var outro = await _produtos.ObterPorNomeAsync(_usuario.GrupoId, nome, ct);
        if (outro is not null && outro.Id != id) throw new ErroDeConflito("Já existe um produto com esse nome.");

        produto.Editar(nome, categoria.Id, requisicao.UnidadePadrao, string.IsNullOrWhiteSpace(requisicao.Descricao) ? null : requisicao.Descricao.Trim());
        await _unidade.SalvarAsync(ct);
        return produto.ParaResposta();
    }
}

/// <summary>
/// Exclusão lógica: some das buscas, mas listas e histórico que usaram o produto não mudam.
/// </summary>
public sealed class ExcluirProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;
    private readonly IRelogio _relogio;

    public ExcluirProduto(IProdutoRepository produtos, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio)
    {
        _produtos = produtos;
        _unidade = unidade;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(Guid id, CancellationToken ct)
    {
        var produto = await _produtos.ObterAsync(_usuario.GrupoId, id, ct) ?? throw new ErroDeNaoEncontrado("Produto não encontrado.");
        produto.Excluir(_relogio.AgoraUtc);
        await _unidade.SalvarAsync(ct);
    }
}
