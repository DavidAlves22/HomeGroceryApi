using System.Net;
using System.Net.Sockets;
using System.Text;
using HomeGroceryApi.Infrastructure.Servicos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeGroceryApi.Tests.Dominio;

public class EnviadorDeEmailSmtpTests
{
    /// <summary>Servidor SMTP mínimo (um e-mail por vez) para provar que o envio real fala SMTP direito, sem provedor externo.</summary>
    private sealed class ServidorSmtpDeTeste : IDisposable
    {
        private readonly TcpListener _escuta = new(IPAddress.Loopback, 0);
        private readonly Task _atendimento;

        public ServidorSmtpDeTeste()
        {
            _escuta.Start();
            _atendimento = Atender();
        }

        public int Porta => ((IPEndPoint)_escuta.LocalEndpoint).Port;
        public string Recebido { get; private set; } = string.Empty;

        public Task Terminou => _atendimento;

        private async Task Atender()
        {
            using var cliente = await _escuta.AcceptTcpClientAsync();
            using var fluxo = cliente.GetStream();
            using var leitor = new StreamReader(fluxo, Encoding.UTF8);
            await using var escritor = new StreamWriter(fluxo, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await escritor.WriteLineAsync("220 localhost ESMTP de teste");

            var dados = new StringBuilder();
            var lendoDados = false;
            while (await leitor.ReadLineAsync() is { } linha)
            {
                if (lendoDados)
                {
                    if (linha == ".")
                    {
                        lendoDados = false;
                        Recebido = dados.ToString();
                        await escritor.WriteLineAsync("250 ok");
                    }
                    else dados.AppendLine(linha);

                    continue;
                }

                var comando = linha.ToUpperInvariant();
                if (comando.StartsWith("EHLO") || comando.StartsWith("HELO")) await escritor.WriteLineAsync("250 localhost");
                else if (comando.StartsWith("MAIL FROM") || comando.StartsWith("RCPT TO")) await escritor.WriteLineAsync("250 ok");
                else if (comando == "DATA")
                {
                    lendoDados = true;
                    await escritor.WriteLineAsync("354 envie");
                }
                else if (comando == "QUIT")
                {
                    await escritor.WriteLineAsync("221 tchau");
                    break;
                }
                else await escritor.WriteLineAsync("250 ok");
            }
        }

        public void Dispose() => _escuta.Stop();
    }

    private static EnviadorDeEmailSmtp Montar(EmailOpcoes opcoes) => new(Options.Create(opcoes), NullLogger<EnviadorDeEmailSmtp>.Instance);

    [Fact]
    public async Task Envia_o_email_pelo_smtp_com_remetente_destinatario_e_texto()
    {
        using var servidor = new ServidorSmtpDeTeste();
        var enviador = Montar(new EmailOpcoes { Host = "127.0.0.1", Porta = servidor.Porta, De = "nao-responda@homegrocery.app", Seguranca = "None" });

        await enviador.EnviarAsync("ana@exemplo.com", "Seu codigo do Home Grocery", "Seu codigo e 123456", default);
        await servidor.Terminou.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(enviador.Habilitado);
        Assert.Contains("ana@exemplo.com", servidor.Recebido);
        Assert.Contains("nao-responda@homegrocery.app", servidor.Recebido);
        Assert.Contains("Subject: Seu codigo do Home Grocery", servidor.Recebido);
        Assert.Contains("123456", servidor.Recebido);
    }

    [Fact]
    public async Task Sem_host_fica_desligado_e_servidor_fora_do_ar_nao_lanca()
    {
        var desligado = Montar(new EmailOpcoes());
        await desligado.EnviarAsync("ana@exemplo.com", "x", "y", default);
        Assert.False(desligado.Habilitado);

        // Porta onde ninguém escuta: a falha é registrada, nunca sobe (o endpoint de recuperação responde igual).
        var foraDoAr = Montar(new EmailOpcoes { Host = "127.0.0.1", Porta = 1, De = "a@b.com", Seguranca = "None" });
        await foraDoAr.EnviarAsync("ana@exemplo.com", "x", "y", default);
    }
}
