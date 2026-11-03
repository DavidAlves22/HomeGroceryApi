using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

public sealed record PontoDto(int X, int Y);

public sealed record AreaDto(int X, int Y, int Largura, int Altura);

public sealed record CriarLojaRequisicao(string Nome, Guid? CopiarDeLojaId = null);

/// <summary>
/// Uma seção do mapa. CategoriasLadoA vale também para seção de um lado só. As ids das seções são sempre
/// geradas pela API: o mapa é salvo por inteiro e a resposta traz as ids novas.
/// </summary>
public sealed record SecaoRequisicao(
    string Nome,
    string? NomeLadoB,
    bool Dividida,
    AreaDto Area,
    IReadOnlyList<Guid>? CategoriasLadoA = null,
    IReadOnlyList<Guid>? CategoriasLadoB = null,
    Guid? IdAnterior = null);

public sealed record SalvarMapaRequisicao(string Nome, int Largura, int Altura, PontoDto Entrada, AreaDto Caixas, IReadOnlyList<SecaoRequisicao> Secoes, int Versao);

public sealed record SecaoResposta(
    Guid Id,
    string Nome,
    string? NomeLadoB,
    bool Dividida,
    AreaDto Area,
    IReadOnlyList<Guid> CategoriasLadoA,
    IReadOnlyList<Guid> CategoriasLadoB);

public sealed record LojaResumoResposta(Guid Id, string Nome, int Largura, int Altura, int Secoes, int Versao);

public sealed record LojaResposta(Guid Id, string Nome, int Largura, int Altura, PontoDto Entrada, AreaDto Caixas, IReadOnlyList<SecaoResposta> Secoes, int Versao);

public sealed record LocalDoProdutoRequisicao(Guid SecaoId, LadoDaSecaoEnum Lado);

/// <summary>Onde o produto do item foi achado neste mercado. Lista vazia = volta a valer a categoria da seção.</summary>
public sealed record DefinirLocalDoItemRequisicao(Guid LojaId, IReadOnlyList<LocalDoProdutoRequisicao> Locais);

/// <summary>Lugar que o usuário marcou para o produto do item (o item é identificado pelo nome na lista).</summary>
public sealed record LocalDoItemResposta(Guid ItemId, Guid SecaoId, LadoDaSecaoEnum Lado);

/// <summary>`UsuarioId`: rota so dessa pessoa (itens das categorias dela + os de todos). Ausente = rota de todos.</summary>
public sealed record RotaRequisicao(Guid LojaId, PontoDto? Posicao = null, Guid? UsuarioId = null);

public sealed record ParadaResposta(
    Guid SecaoId,
    string Nome,
    LadoDaSecaoEnum? Lado,
    string? LadoTexto,
    PontoDto Posicao,
    double DistanciaEmMetros,
    IReadOnlyList<ItemDaListaResposta> Itens);

/// <summary>Motivo: "SemSecao" (o mercado não tem lugar para a categoria) ou "NaoTemNaLoja" (marcado como "Não tem aqui").</summary>
public sealed record ItemForaDaRotaResposta(ItemDaListaResposta Item, string Motivo);

/// <summary>Item pendente que ficou de fora da rota porque outra pessoa vai pegar.</summary>
public sealed record ItemDeOutraPessoaResposta(ItemDaListaResposta Item, Guid UsuarioId);

public sealed record RotaResposta(
    Guid LojaId,
    PontoDto Posicao,
    IReadOnlyList<ParadaResposta> Paradas,
    IReadOnlyList<PontoDto> Trilha,
    int DistanciaEmMetros,
    int EconomiaEmMetros,
    int TempoEstimadoEmMinutos,
    IReadOnlyList<ItemForaDaRotaResposta> ForaDaRota,
    int JaCompradosEmOutrosMercados,
    IReadOnlyList<LocalDoItemResposta> LocaisProprios,
    IReadOnlyList<ItemDeOutraPessoaResposta> ComOutrasPessoas);
