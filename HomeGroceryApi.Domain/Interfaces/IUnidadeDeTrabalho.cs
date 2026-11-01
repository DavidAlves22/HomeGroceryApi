namespace HomeGroceryApi.Domain.Interfaces;

/// <summary>
/// Confirma de uma vez tudo que os repositórios registraram (uma transação por caso de uso).
/// </summary>
public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Executa a ação numa transação de banco: ou tudo é gravado, ou nada.
    /// </summary>
    Task ExecutarEmTransacaoAsync(Func<Task> acao, CancellationToken ct);
}
