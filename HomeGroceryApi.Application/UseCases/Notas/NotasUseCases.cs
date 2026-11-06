using System.Security.Cryptography;
using System.Text;
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Listas;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Notas;

internal static class CnpjDaNota
{
    /// <summary>Só os 14 dígitos: "93.209.765/0509-98" e "93209765050998" são o mesmo mercado.</summary>
    public static string? Normalizar(string? cnpj)
    {
        var digitos = new string((cnpj ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length == 0 ? null : digitos.Length == 14 ? digitos : throw new ErroDeValidacao("O CNPJ do mercado na nota está incompleto.");
    }
}

internal static class ChaveDaNota
{
    /// <summary>Chave de acesso da NFC-e: 44 dígitos. O CNPJ do emitente são os dígitos 7 a 20.</summary>
    public static string? Normalizar(string? chave)
    {
        var digitos = new string((chave ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length == 0 ? null : digitos.Length == 44 ? digitos : throw new ErroDeValidacao("A chave de acesso da nota deve ter 44 dígitos.");
    }

    public static string CnpjDoEmitente(string chave) => chave.Substring(6, 14);
}

/// <summary>"Esta nota já foi importada?": o app pergunta logo depois de ler o QR, antes de pedir o texto.</summary>
public sealed class ConsultarNotaImportada
{
    private readonly INotaFiscalRepository _notas;
    private readonly IUsuarioAtual _usuario;

    public ConsultarNotaImportada(INotaFiscalRepository notas, IUsuarioAtual usuario)
    {
        _notas = notas;
        _usuario = usuario;
    }

    public async Task<NotaImportadaResposta> ExecutarAsync(string? chave, CancellationToken ct)
    {
        var normalizada = ChaveDaNota.Normalizar(chave) ?? throw new ErroDeValidacao("Informe a chave de acesso da nota.");
        return new NotaImportadaResposta(await _notas.ExisteNotaPorChaveAsync(_usuario.GrupoId, normalizada, ct));
    }
}

/// <summary>Pares "código do mercado = produto" que o grupo já ensinou para este CNPJ.</summary>
public sealed class ObterVinculosDaNota
{
    private readonly INotaFiscalRepository _notas;
    private readonly IUsuarioAtual _usuario;

    public ObterVinculosDaNota(INotaFiscalRepository notas, IUsuarioAtual usuario)
    {
        _notas = notas;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<VinculoDaNotaResposta>> ExecutarAsync(string? cnpj, CancellationToken ct)
    {
        var normalizado = CnpjDaNota.Normalizar(cnpj);
        if (normalizado is null) return [];
        return (await _notas.ListarVinculosAsync(_usuario.GrupoId, normalizado, ct)).Select(v => new VinculoDaNotaResposta(v.Codigo, v.ProdutoId)).ToList();
    }
}

/// <summary>
/// Importa uma nota fiscal já lida pelo app: os itens casados com a lista recebem o preço pago e a quantidade comprada e
/// viram comprados; os que não estão na lista entram já comprados. Tudo numa gravação só. A mesma nota (mesmo CNPJ e mesmos
/// itens) não entra duas vezes, e os pares código/produto ficam guardados para as próximas notas do mesmo mercado.
/// </summary>
public sealed class ImportarNotaNaLista : CasoDeUsoDeLista
{
    private const int MaximoDeItens = 200;

    private readonly INotaFiscalRepository _notas;
    private readonly ILojaRepository _lojas;
    private readonly CatalogoDeProdutos _catalogo;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public ImportarNotaNaLista(IListaRepository listas, INotaFiscalRepository notas, ILojaRepository lojas, CatalogoDeProdutos catalogo, IUnidadeDeTrabalho unidade, IUsuarioAtual usuario, IRelogio relogio) : base(listas, usuario)
    {
        _notas = notas;
        _lojas = lojas;
        _catalogo = catalogo;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<ListaResposta> ExecutarAsync(Guid listaId, ImportarNotaRequisicao requisicao, CancellationToken ct)
    {
        var pedidos = requisicao.Itens ?? [];
        if (pedidos.Count is 0 or > MaximoDeItens) throw new ErroDeValidacao($"A nota precisa ter de 1 a {MaximoDeItens} itens.");
        var cnpj = CnpjDaNota.Normalizar(requisicao.Cnpj);
        var chave = ChaveDaNota.Normalizar(requisicao.ChaveDeAcesso);
        if (chave is not null && cnpj is not null && ChaveDaNota.CnpjDoEmitente(chave) != cnpj) throw new ErroDeValidacao("O QR lido não é da mesma nota (o CNPJ não confere).");
        cnpj ??= chave is null ? null : ChaveDaNota.CnpjDoEmitente(chave);

        // Vale também para lista finalizada: o jeito normal é importar as notas em casa, depois de fechar a compra.
        var lista = await ObterListaAsync(listaId, ct);

        Loja? lojaDaNota = null;
        if (requisicao.LojaId is { } lojaId)
        {
            lojaDaNota = await _lojas.ObterAsync(Usuario.GrupoId, lojaId, ct);
            if (lojaDaNota is null || lojaDaNota.ExcluidoEm is not null) throw new ErroDeValidacao("Escolha um mercado válido.");
        }

        foreach (var pedido in pedidos)
        {
            Entrada.Quantidade(pedido.Quantidade);
            if (Entrada.Preco(pedido.PrecoUnitario, "O preço pago") is null) throw new ErroDeValidacao("Informe o preço pago de cada item.");
            if (pedido.ItemId is null && pedido.CategoriaId is null) throw new ErroDeValidacao("Escolha a categoria dos itens novos.");
            if (pedido.Codigo is { Length: > 40 }) throw new ErroDeValidacao("Código de produto inválido na nota.");
        }

        // A impressão digital usa só o que identifica a compra: CNPJ e, por item, código, quantidade e preço.
        var impressao = Impressao(cnpj, pedidos);
        var total = pedidos.Sum(p => decimal.Round(p.Quantidade * p.PrecoUnitario, 2, MidpointRounding.AwayFromZero));
        if (await _notas.ExisteNotaAsync(Usuario.GrupoId, impressao, ct) || (chave is not null && await _notas.ExisteNotaPorChaveAsync(Usuario.GrupoId, chave, ct))) throw new ErroDeConflito("Esta nota já foi importada.");

        var itensDaLista = (await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct)).ToDictionary(i => i.Id);
        var vinculos = cnpj is null ? [] : (await _notas.ListarVinculosAsync(Usuario.GrupoId, cnpj, ct)).ToDictionary(v => v.Codigo);
        var agora = _relogio.AgoraUtc;
        var ordem = await Listas.ProximaOrdemDeItemAsync(listaId, ct);
        var itensNovos = new List<ItemDaLista>();

        // Duas linhas da nota para o mesmo item da lista (ex.: pesagens) viram uma compra só, com o preço médio.
        foreach (var grupo in pedidos.Where(p => p.ItemId is not null).GroupBy(p => p.ItemId!.Value))
        {
            if (!itensDaLista.TryGetValue(grupo.Key, out var item)) throw new ErroDeNaoEncontrado("Um item da lista não foi encontrado.");
            var quantidade = grupo.Sum(p => p.Quantidade);
            var valor = grupo.Sum(p => p.Quantidade * p.PrecoUnitario);
            item.RegistrarCompra(quantidade, decimal.Round(valor / quantidade, 2, MidpointRounding.AwayFromZero), Usuario.UsuarioId, agora, requisicao.LojaId, grupo.First().Unidade);
            await GuardarVinculoAsync(cnpj, grupo.Select(p => p.Codigo), item.ProdutoId, vinculos, ct);
        }

        foreach (var pedido in pedidos.Where(p => p.ItemId is null))
        {
            var nome = Entrada.Nome(pedido.Nome);
            var unidade = pedido.Unidade ?? UnidadeDeMedidaEnum.Unidade;
            var (produto, categoria) = await _catalogo.ResolverAsync(Usuario.GrupoId, nome, pedido.CategoriaId!.Value, unidade, ct);
            var novo = ItemDaLista.Criar(Guid.CreateVersion7(), listaId, lista.GrupoId, produto.Id, nome, categoria.Id, categoria.Nome, unidade, pedido.Quantidade, ordem++, Usuario.UsuarioId);
            novo.RegistrarCompra(pedido.Quantidade, pedido.PrecoUnitario, Usuario.UsuarioId, agora, requisicao.LojaId);
            await Listas.AdicionarItemAsync(novo, ct);
            itensNovos.Add(novo);
            await GuardarVinculoAsync(cnpj, [pedido.Codigo], produto.Id, vinculos, ct);
        }

        // O CNPJ fica no mercado escolhido: a próxima nota dele já abre no mercado certo.
        if (lojaDaNota is not null && cnpj is not null) lojaDaNota.DefinirCnpj(cnpj);

        // Em compra finalizada o total é congelado: recalcula com o que a nota acrescentou (tudo na mesma gravação).
        if (lista.Finalizada) lista.AtualizarTotalGastoFinal(itensDaLista.Values.Concat(itensNovos).Sum(CalculoDeTotais.TotalRealizadoDoItem));

        await _notas.AdicionarNotaAsync(NotaImportada.Criar(Guid.CreateVersion7(), Usuario.GrupoId, listaId, cnpj ?? string.Empty, impressao, total, Usuario.UsuarioId, agora, chave), ct);
        await _unidade.SalvarAsync(ct);

        return lista.ParaListaResposta(await Listas.ListarItensAsync(Usuario.GrupoId, listaId, ct));
    }

    private async Task GuardarVinculoAsync(string? cnpj, IEnumerable<string?> codigos, Guid? produtoId, Dictionary<string, VinculoDeProdutoDaNota> vinculos, CancellationToken ct)
    {
        if (cnpj is null || produtoId is null) return;
        foreach (var codigo in codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).Distinct())
        {
            if (vinculos.TryGetValue(codigo, out var existente))
            {
                existente.ApontarPara(produtoId.Value);
                continue;
            }

            var novo = VinculoDeProdutoDaNota.Criar(Guid.CreateVersion7(), Usuario.GrupoId, cnpj, codigo, produtoId.Value);
            vinculos[codigo] = novo;
            await _notas.AdicionarVinculoAsync(novo, ct);
        }
    }

    private static string Impressao(string? cnpj, IReadOnlyList<ItemDaNotaRequisicao> itens)
    {
        var texto = $"{cnpj}|" + string.Join(';', itens.Select(i => $"{i.Codigo?.Trim().ToUpperInvariant()}:{i.Quantidade:0.###}:{i.PrecoUnitario:0.##}").OrderBy(x => x, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }
}
