namespace HomeGroceryApi.Application.Dtos;

public sealed record MembroResposta(Guid UsuarioId, string Nome, string Papel, bool Voce, DateTime EntrouEm);

public sealed record GrupoResposta(Guid Id, string Nome, bool Compartilhado, string MeuPapel, IReadOnlyList<MembroResposta> Membros);

public sealed record ConviteResposta(string Codigo, DateTime ExpiraEm);

public sealed record EntrarNoGrupoRequisicao(string Codigo);
