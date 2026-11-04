using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Tests.Fakes;

/// <summary>Guarda os e-mails que seriam enviados, para os testes lerem o código de recuperação (sem SMTP de verdade).</summary>
public sealed class EnviadorDeEmailDeTeste : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<(string Para, string Assunto, string Texto)> _enviados = new();

    public bool Habilitado { get; set; } = true;

    public IReadOnlyList<(string Para, string Assunto, string Texto)> Enviados => _enviados.ToArray();

    public Task EnviarAsync(string para, string assunto, string texto, CancellationToken ct)
    {
        _enviados.Enqueue((para, assunto, texto));
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string Para, string Assunto, string Texto)> Para(string email) => Enviados.Where(e => e.Para.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>O código de 6 dígitos do último e-mail enviado a este endereço.</summary>
    public string UltimoCodigo(string email) => Regex.Match(Para(email).Last().Texto, @"\b(\d{6})\b").Groups[1].Value;
}
