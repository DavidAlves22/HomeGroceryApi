namespace HomeGroceryApi.Application.Dtos;

public sealed record AdminUsuariosResumo(int Total, int Novos7Dias, int Novos30Dias, int Ativos24Horas, int Ativos7Dias, int Ativos30Dias);

public sealed record AdminGruposResumo(int Total, int Familias);

public sealed record AdminListasResumo(int Total, int EmAndamento, int Finalizadas, decimal GastoTotalFinalizado);

public sealed record AdminItensResumo(int Total, int Comprados);

public sealed record AdminResumoResposta(
    DateTime GeradoEm,
    AdminUsuariosResumo Usuarios,
    AdminGruposResumo Grupos,
    AdminListasResumo Listas,
    AdminItensResumo Itens,
    int Mercados,
    int NotasImportadas,
    int AparelhosComAvisos);

/// <summary>Um ponto da série: o dia (AAAA-MM-DD, horário de Brasília) e quanto aconteceu nele.</summary>
public sealed record AdminPontoDoDia(string Dia, int Valor);

public sealed record AdminSeriesResposta(
    int Dias,
    IReadOnlyList<AdminPontoDoDia> Cadastros,
    IReadOnlyList<AdminPontoDoDia> ListasCriadas,
    IReadOnlyList<AdminPontoDoDia> ItensComprados);

public sealed record AdminUsuarioResposta(
    Guid Id,
    string Nome,
    string Email,
    DateTime CriadoEm,
    DateTime? UltimoAcessoEm,
    string Grupo,
    int MembrosDoGrupo,
    int ListasCriadas,
    int ItensComprados,
    DateTime? BloqueadoEm = null,
    bool EhAdmin = false);

public sealed record AdminBloqueioRequisicao(bool Bloqueado);

public sealed record AdminUsuariosResposta(int Total, int Pagina, int Tamanho, IReadOnlyList<AdminUsuarioResposta> Itens);

/// <summary>Das contas criadas há pelo menos <paramref name="Dias"/> dias, quantas ainda abriam o app N dias depois do cadastro.</summary>
public sealed record AdminRetencaoLinha(int Dias, int Elegiveis, int Voltaram);

public sealed record AdminFunil(int Contas, int CriaramLista, int Compraram, int Finalizaram, int SemAtividade);

public sealed record AdminGastoDoMes(string Competencia, decimal Total, int Compras);

public sealed record AdminGasto(int Compras, decimal Medio, IReadOnlyList<AdminGastoDoMes> PorMes);

/// <summary>Posição de um ranking. <paramref name="Detalhe"/> é um segundo número opcional (ex.: em quantas casas o produto aparece).</summary>
public sealed record AdminItemDeRanking(string Nome, int Quantidade, int? Detalhe);

/// <summary>Quantos usaram um recurso (<paramref name="Recurso"/> é uma chave que o app traduz) entre <paramref name="De"/> contas ou grupos.</summary>
public sealed record AdminAdocao(string Recurso, int Quantidade, int De, string Base);

public sealed record AdminInsightsResposta(
    IReadOnlyList<AdminRetencaoLinha> Retencao,
    AdminFunil Funil,
    AdminGasto Gasto,
    IReadOnlyList<AdminItemDeRanking> ProdutosMaisComprados,
    IReadOnlyList<AdminItemDeRanking> CategoriasMaisCompradas,
    IReadOnlyList<AdminItemDeRanking> CidadesComMercados,
    IReadOnlyList<AdminItemDeRanking> MapasMaisCopiados,
    IReadOnlyList<AdminAdocao> Adocao);
