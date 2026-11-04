namespace HomeGroceryApi.Application.Dtos;

public sealed record EsqueciSenhaRequisicao(string? Email);

public sealed record RedefinirSenhaRequisicao(string? Email, string? Codigo, string? NovaSenha);

public sealed record RecuperacaoResposta(bool Disponivel);

public sealed record AlterarNomeRequisicao(string? Nome);

public sealed record LoginGoogleRequisicao(string? IdToken);

public sealed record GoogleDisponivelResposta(string? ClienteId);
