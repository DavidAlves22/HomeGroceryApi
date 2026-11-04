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
    int ItensComprados);

public sealed record AdminUsuariosResposta(int Total, int Pagina, int Tamanho, IReadOnlyList<AdminUsuarioResposta> Itens);
