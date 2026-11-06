using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Application.Dtos;

/// <summary>Uma informação do card de contato. Descrição nula = a tela não mostra a linha.</summary>
public sealed record InformacaoDeContatoResposta(Guid Id, string Titulo, string? Descricao, string Valor);

public sealed record ContatoResposta(IReadOnlyList<InformacaoDeContatoResposta> Informacoes);

public sealed record InformacaoDeContatoRequisicao(string? Titulo, string? Descricao, string? Valor);

public sealed record EnviarMensagemRequisicao(TipoDeMensagemEnum Tipo, string? Assunto, string? Texto);

/// <param name="RespostaNova">Há resposta do administrador que a pessoa ainda não abriu.</param>
public sealed record MensagemResposta(Guid Id, TipoDeMensagemEnum Tipo, string Assunto, string Texto, StatusDaMensagemEnum Status, DateTime CriadoEm, DateTime AtualizadoEm, string? Resposta, DateTime? RespondidoEm, bool RespostaNova);

public sealed record MensagensResposta(IReadOnlyList<MensagemResposta> Itens, int RespostasNovas);

/// <summary>A mensagem como o administrador a vê: com quem enviou.</summary>
public sealed record AdminMensagemResposta(Guid Id, Guid UsuarioId, string Nome, string Email, TipoDeMensagemEnum Tipo, string Assunto, string Texto, StatusDaMensagemEnum Status, DateTime CriadoEm, DateTime AtualizadoEm, string? Resposta, DateTime? RespondidoEm);

/// <param name="AguardandoResposta">Mensagens "Recebida" ou "Em análise" (de todas as pessoas, não só as listadas).</param>
public sealed record AdminMensagensResposta(IReadOnlyList<InformacaoDeContatoResposta> Informacoes, int AguardandoResposta, IReadOnlyList<AdminMensagemResposta> Itens);

public sealed record AdminResponderMensagemRequisicao(string? Resposta, StatusDaMensagemEnum Status);
