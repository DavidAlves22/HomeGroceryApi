namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// Envia e-mail (hoje só o código de recuperação de senha). Sem o servidor SMTP configurado fica desligado:
/// `Habilitado` é falso e a recuperação de senha nem aparece no app. A implementação não lança: falha de envio é
/// registrada no log, para o endpoint não revelar (nem quebrar por) um e-mail que não saiu.
/// </summary>
public interface IEnviadorDeEmail
{
    bool Habilitado { get; }
    Task EnviarAsync(string para, string assunto, string texto, CancellationToken ct);
}
