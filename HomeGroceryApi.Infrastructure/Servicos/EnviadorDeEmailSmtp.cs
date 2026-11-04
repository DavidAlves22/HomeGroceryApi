using HomeGroceryApi.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>
/// Servidor SMTP que envia os e-mails (qualquer provedor com plano grátis: Brevo, Gmail com senha de app...). Sem `Host` e
/// `De` configurados o envio fica desligado e a recuperação de senha nem aparece no app.
/// </summary>
public sealed class EmailOpcoes
{
    public const string Secao = "Email";
    public string Host { get; set; } = string.Empty;
    public int Porta { get; set; } = 587;
    public string Usuario { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;

    /// <summary>Endereço que aparece como remetente (precisa ser aceito pelo provedor).</summary>
    public string De { get; set; } = string.Empty;

    public string NomeDoRemetente { get; set; } = "Home Grocery";

    /// <summary>Auto (padrão): usa STARTTLS quando o servidor oferece. SslOnConnect para a porta 465; None só para testes locais.</summary>
    public string Seguranca { get; set; } = "Auto";
}

public sealed class EnviadorDeEmailSmtp : IEnviadorDeEmail
{
    private readonly EmailOpcoes _opcoes;
    private readonly ILogger<EnviadorDeEmailSmtp> _log;

    public EnviadorDeEmailSmtp(IOptions<EmailOpcoes> opcoes, ILogger<EnviadorDeEmailSmtp> log)
    {
        _opcoes = opcoes.Value;
        _log = log;
    }

    public bool Habilitado => !string.IsNullOrWhiteSpace(_opcoes.Host) && !string.IsNullOrWhiteSpace(_opcoes.De);

    public async Task EnviarAsync(string para, string assunto, string texto, CancellationToken ct)
    {
        if (!Habilitado) return;

        try
        {
            var mensagem = new MimeMessage();
            mensagem.From.Add(new MailboxAddress(_opcoes.NomeDoRemetente, _opcoes.De));
            mensagem.To.Add(MailboxAddress.Parse(para));
            mensagem.Subject = assunto;
            mensagem.Body = new TextPart("plain") { Text = texto };

            using var cliente = new SmtpClient();
            var seguranca = Enum.TryParse<SecureSocketOptions>(_opcoes.Seguranca, true, out var escolhida) ? escolhida : SecureSocketOptions.Auto;
            await cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, seguranca, ct);
            if (!string.IsNullOrEmpty(_opcoes.Usuario)) await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha, ct);
            await cliente.SendAsync(mensagem, ct);
            await cliente.DisconnectAsync(true, ct);
        }
        catch (Exception erro)
        {
            // Nunca derruba a requisição nem revela por ela se o e-mail existe: só registra (sem o código nem a senha do SMTP).
            _log.LogWarning(erro, "Falha ao enviar e-mail de recuperação");
        }
    }
}
