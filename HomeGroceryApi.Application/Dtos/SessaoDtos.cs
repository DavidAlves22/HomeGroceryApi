namespace HomeGroceryApi.Application.Dtos;

public sealed record CadastroRequisicao(string Nome, string Email, string Senha);

public sealed record LoginRequisicao(string Email, string Senha);

public sealed record RenovarSessaoRequisicao(string TokenDeRenovacao);

public sealed record UsuarioResposta(Guid Id, string Nome, string Email);

public sealed record SessaoResposta(
    string TokenDeAcesso,
    DateTime ExpiraEm,
    string TokenDeRenovacao,
    UsuarioResposta Usuario,
    Guid GrupoId,
    bool EhAdmin = false);
