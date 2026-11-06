using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Excecoes;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Feedback ou solicitação que uma pessoa manda ao administrador do app. Pertence à PESSOA, não ao grupo: a família não vê
/// o que um membro escreveu. Guarda nome e e-mail de quem enviou (snapshot) para o administrador saber quem é mesmo que a
/// conta mude depois. A resposta é uma só (o administrador pode reescrevê-la); quem quiser continuar a conversa abre outra.
/// </summary>
public sealed class MensagemDeContato
{
    public const int AssuntoMaximo = 100;
    public const int TextoMaximo = 2000;
    public const int RespostaMaxima = 2000;

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string NomeDoUsuario { get; private set; } = string.Empty;
    public string EmailDoUsuario { get; private set; } = string.Empty;
    public TipoDeMensagemEnum Tipo { get; private set; }
    public string Assunto { get; private set; } = string.Empty;
    public string Texto { get; private set; } = string.Empty;
    public StatusDaMensagemEnum Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public string? Resposta { get; private set; }
    public DateTime? RespondidoEm { get; private set; }

    /// <summary>Quando a pessoa abriu a resposta atual. Nulo com resposta = "resposta nova" (a tela avisa).</summary>
    public DateTime? RespostaVistaEm { get; private set; }

    private MensagemDeContato()
    {
    }

    public static MensagemDeContato Criar(Guid id, Guid usuarioId, string nome, string email, TipoDeMensagemEnum tipo, string assunto, string texto, DateTime agora)
    {
        return new MensagemDeContato
        {
            Id = id,
            UsuarioId = usuarioId,
            NomeDoUsuario = nome,
            EmailDoUsuario = email,
            Tipo = tipo,
            Assunto = assunto,
            Texto = texto,
            Status = StatusDaMensagemEnum.Recebida,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public bool TemRespostaNova => Resposta is not null && RespostaVistaEm is null;

    /// <summary>
    /// Resposta e status do administrador. Escrever (ou mudar) a resposta a marca como não vista pela pessoa. Marcar como
    /// "Respondida" sem resposta escrita não faz sentido, então é recusado.
    /// </summary>
    public void Responder(string? resposta, StatusDaMensagemEnum status, DateTime agora)
    {
        var texto = string.IsNullOrWhiteSpace(resposta) ? null : resposta.Trim();
        if (texto is null && Resposta is null && status == StatusDaMensagemEnum.Respondida) throw new ErroDeDominio("Escreva a resposta antes de marcar como respondida.");

        if (texto is not null && texto != Resposta)
        {
            Resposta = texto;
            RespondidoEm = agora;
            RespostaVistaEm = null;
        }

        Status = status;
        AtualizadoEm = agora;
    }

    public void MarcarRespostaComoVista(DateTime agora)
    {
        if (Resposta is not null) RespostaVistaEm ??= agora;
    }
}
