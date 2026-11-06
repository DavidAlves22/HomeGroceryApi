using HomeGroceryApi.Domain.Entidades;

namespace HomeGroceryApi.Domain.Interfaces;

public interface IContatoRepository
{
    Task AdicionarAsync(MensagemDeContato mensagem, CancellationToken ct);

    /// <summary>Com rastreamento: o caso de uso altera e a unidade de trabalho grava.</summary>
    Task<MensagemDeContato?> ObterAsync(Guid id, CancellationToken ct);

    Task<List<MensagemDeContato>> ListarDoUsuarioAsync(Guid usuarioId, int limite, CancellationToken ct);

    /// <summary>Todas (de todas as pessoas), da mais recente para a mais antiga. Só o administrador chama.</summary>
    Task<List<MensagemDeContato>> ListarTodasAsync(int limite, CancellationToken ct);

    Task<int> ContarAguardandoRespostaAsync(CancellationToken ct);

    /// <summary>Quantas a pessoa enviou desde o instante dado (trava de abuso).</summary>
    Task<int> ContarEnviadasDesdeAsync(Guid usuarioId, DateTime desde, CancellationToken ct);

    /// <summary>As informações do card de contato, na ordem em que o administrador as criou.</summary>
    Task<List<InformacaoDeContato>> ListarInformacoesAsync(CancellationToken ct);

    Task<InformacaoDeContato?> ObterInformacaoAsync(Guid id, CancellationToken ct);

    Task AdicionarInformacaoAsync(InformacaoDeContato informacao, CancellationToken ct);

    void RemoverInformacao(InformacaoDeContato informacao);
}
