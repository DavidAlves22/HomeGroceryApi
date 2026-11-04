using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Lojas;

/// <summary>
/// Catálogo de mapas: todo mapa de mercado criado por qualquer grupo fica disponível para outros grupos COPIAREM.
/// A cópia é independente (quem copia edita a sua; o original nunca muda por mão alheia). O catálogo não revela quem criou.
/// O que é do grupo (locais de produto, listas, compras) nunca sai dele: só o desenho e as categorias de cada seção.
/// </summary>
public sealed class ListarCatalogoDeMercados
{
    private const int Limite = 50;

    private readonly ILojaRepository _lojas;
    private readonly IUsuarioAtual _usuario;

    public ListarCatalogoDeMercados(ILojaRepository lojas, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<CatalogoMercadoResposta>> ExecutarAsync(string? busca, string? cidade, CancellationToken ct)
    {
        var itens = await _lojas.ListarCatalogoAsync(_usuario.GrupoId, busca, cidade, Limite, ct);
        return itens.Select(i => new CatalogoMercadoResposta(i.Loja.Id, i.Loja.Nome, i.Loja.Largura, i.Loja.Altura, i.Secoes, i.Copias, i.Loja.Cidade)).ToList();
    }
}

public sealed class ObterMercadoDoCatalogo
{
    private readonly ILojaRepository _lojas;
    private readonly ICategoriaRepository _categorias;

    public ObterMercadoDoCatalogo(ILojaRepository lojas, ICategoriaRepository categorias)
    {
        _lojas = lojas;
        _categorias = categorias;
    }

    public async Task<CatalogoMercadoDetalheResposta> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var loja = await _lojas.ObterDoCatalogoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Mapa não encontrado.");
        var secoes = await _lojas.ListarSecoesDoCatalogoAsync(id, ct);
        var vinculos = await _lojas.ListarVinculosDoCatalogoAsync(id, ct);
        var nomes = (await _categorias.ListarAsync(loja.GrupoId, true, ct)).ToDictionary(c => c.Id, c => c.Nome);

        List<string> Lado(Guid secaoId, LadoDaSecaoEnum lado) => vinculos.Where(v => v.SecaoId == secaoId && v.Lado == lado && nomes.ContainsKey(v.CategoriaId)).Select(v => nomes[v.CategoriaId]).ToList();

        var respostas = secoes.Select(s => new CatalogoSecaoResposta(s.Nome, s.NomeLadoB, s.Dividida, s.Area.ParaDto(), Lado(s.Id, LadoDaSecaoEnum.A), Lado(s.Id, LadoDaSecaoEnum.B))).ToList();
        return new CatalogoMercadoDetalheResposta(loja.Id, loja.Nome, loja.Largura, loja.Altura, loja.Entrada.ParaDto(), loja.Caixas.ParaDto(), respostas, await _lojas.ContarCopiasAsync(id, ct), loja.Cidade);
    }
}

/// <summary>
/// Cria no grupo de quem pediu um mercado novo com o desenho do mapa do catálogo. As categorias são procuradas pelo NOME no
/// grupo e as que faltam são criadas; os locais de produto NÃO vêm (são dados do grupo de origem).
/// </summary>
public sealed class CopiarMercadoDoCatalogo
{
    private readonly ILojaRepository _lojas;
    private readonly ICategoriaRepository _categorias;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public CopiarMercadoDoCatalogo(ILojaRepository lojas, ICategoriaRepository categorias, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _categorias = categorias;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<LojaResposta> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var grupoId = _usuario.GrupoId;
        var origem = await _lojas.ObterDoCatalogoAsync(id, ct) ?? throw new ErroDeNaoEncontrado("Mapa não encontrado.");
        var secoesOrigem = await _lojas.ListarSecoesDoCatalogoAsync(id, ct);
        if (secoesOrigem.Count == 0) throw new ErroDeNaoEncontrado("Mapa não encontrado.");
        var vinculosOrigem = await _lojas.ListarVinculosDoCatalogoAsync(id, ct);
        var nomesDaOrigem = (await _categorias.ListarAsync(origem.GrupoId, true, ct)).ToDictionary(c => c.Id, c => c.Nome);

        var minhas = await _categorias.ListarAsync(grupoId, false, ct);
        var idPorNome = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in minhas) idPorNome.TryAdd(c.Nome, c.Id);
        var proximaOrdem = minhas.Count == 0 ? 0 : minhas.Max(c => c.Ordem) + 1;

        async Task<Guid?> CategoriaDoGrupoAsync(Guid categoriaDaOrigem)
        {
            if (!nomesDaOrigem.TryGetValue(categoriaDaOrigem, out var nome)) return null;
            if (idPorNome.TryGetValue(nome, out var existente)) return existente;

            var nova = Categoria.Criar(Guid.CreateVersion7(), grupoId, nome, proximaOrdem++, false);
            await _categorias.AdicionarAsync(nova, ct);
            idPorNome[nome] = nova.Id;
            return nova.Id;
        }

        var existentes = await _lojas.ListarAsync(grupoId, false, ct);
        var nomeDaCopia = NomeLivre(origem.Nome, existentes.Select(l => l.Nome));
        var loja = Loja.Criar(Guid.CreateVersion7(), grupoId, nomeDaCopia, origem.Largura, origem.Altura, origem.Entrada, origem.Caixas, origem.Id, origem.Cidade);

        var secoes = new List<Secao>();
        var vinculos = new List<SecaoCategoria>();
        var novaPorAntiga = new Dictionary<Guid, Secao>();
        foreach (var s in secoesOrigem)
        {
            var nova = Secao.Criar(Guid.CreateVersion7(), loja.Id, grupoId, s.Nome, s.NomeLadoB, s.Dividida, s.Area);
            novaPorAntiga[s.Id] = nova;
            secoes.Add(nova);
        }

        foreach (var v in vinculosOrigem)
        {
            if (!novaPorAntiga.TryGetValue(v.SecaoId, out var secao)) continue;
            if (await CategoriaDoGrupoAsync(v.CategoriaId) is { } categoriaId) vinculos.Add(SecaoCategoria.Criar(Guid.CreateVersion7(), secao.Id, loja.Id, grupoId, categoriaId, v.Lado));
        }

        await _lojas.AdicionarAsync(loja, ct);
        await _lojas.AdicionarMapaAsync(secoes, vinculos, ct);
        await _unidade.SalvarAsync(ct);
        return loja.ParaResposta(secoes, vinculos);
    }

    /// <summary>Nome do mapa; se o grupo já tem um mercado com ele, "Nome (2)", "Nome (3)"...</summary>
    private static string NomeLivre(string nome, IEnumerable<string> usados)
    {
        var existentes = usados.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existentes.Contains(nome)) return nome;

        for (var numero = 2; ; numero++)
        {
            var sufixo = $" ({numero})";
            var candidato = (nome.Length + sufixo.Length > 120 ? nome[..(120 - sufixo.Length)] : nome) + sufixo;
            if (!existentes.Contains(candidato)) return candidato;
        }
    }
}

/// <summary>Liga ou desliga a presença do mapa do grupo no catálogo. Quem já copiou continua com a sua cópia.</summary>
public sealed class CompartilharLoja
{
    private readonly ILojaRepository _lojas;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _usuario;

    public CompartilharLoja(ILojaRepository lojas, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario)
    {
        _lojas = lojas;
        _unidade = unidade;
        _usuario = usuario;
    }

    public async Task<LojaResumoResposta> ExecutarAsync(Guid id, CompartilharLojaRequisicao requisicao, CancellationToken ct)
    {
        var loja = await _lojas.ObterAsync(_usuario.GrupoId, id, ct);
        if (loja is null || loja.ExcluidoEm is not null) throw new ErroDeNaoEncontrado("Mercado não encontrado.");

        loja.DefinirCompartilhada(requisicao.Compartilhada);
        await _unidade.SalvarAsync(ct);
        var secoes = await _lojas.ListarSecoesAsync(_usuario.GrupoId, id, ct);
        return loja.ParaResumo(secoes.Count);
    }
}
