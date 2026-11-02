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
    IReadOnlyList<Guid>? CategoriasLadoB = null);

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

public sealed record RotaRequisicao(Guid LojaId, PontoDto? Posicao = null);

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

public sealed record RotaResposta(
    Guid LojaId,
    PontoDto Posicao,
    IReadOnlyList<ParadaResposta> Paradas,
    IReadOnlyList<PontoDto> Trilha,
    int DistanciaEmMetros,
    int EconomiaEmMetros,
    int TempoEstimadoEmMinutos,
    IReadOnlyList<ItemForaDaRotaResposta> ForaDaRota,
    int JaCompradosEmOutrosMercados);
