using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Excecoes;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Lista de compras de um mês. Independente da lista padrão e de outros meses.
/// Finalizada = somente leitura até ser reaberta, para ninguém alterar o histórico sem querer.
/// </summary>
public sealed class ListaDeCompras
{
    public Guid Id { get; private set; }
    public Guid GrupoId { get; private set; }
    public string Competencia { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public bool Principal { get; private set; }
    public StatusDaListaEnum Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public Guid CriadoPorUsuarioId { get; private set; }
    public DateTime? FinalizadaEm { get; private set; }

    /// <summary>Total efetivamente gasto, congelado no momento em que a compra é finalizada.</summary>
    public decimal? TotalGastoFinal { get; private set; }

    /// <summary>
    /// Quem pega cada categoria nesta ida ao mercado (categoria -> usuario). Categoria fora do mapa = de todos.
    /// Vale so para esta lista: a proxima compra comeca sem divisao.
    /// </summary>
    public Dictionary<Guid, Guid> ResponsaveisPorCategoria { get; private set; } = [];

    private ListaDeCompras()
    {
    }

    public static ListaDeCompras Criar(Guid id, Guid grupoId, string competencia, string nome, bool principal, Guid usuarioId, DateTime agora)
    {
        return new ListaDeCompras { Id = id, GrupoId = grupoId, Competencia = competencia, Nome = nome, Principal = principal, Status = StatusDaListaEnum.EmAndamento, CriadoEm = agora, CriadoPorUsuarioId = usuarioId };
    }

    public bool Finalizada => Status == StatusDaListaEnum.Finalizada;

    public void GarantirEditavel()
    {
        if (Finalizada) throw new ErroDeDominio("Esta lista já foi finalizada. Reabra a lista para alterá-la.");
    }

    /// <summary>Troca a divisao inteira; mapa vazio desfaz a divisao.</summary>
    public void DefinirDivisao(IReadOnlyDictionary<Guid, Guid> responsaveis)
    {
        GarantirEditavel();
        // Troca o dicionario inteiro (em vez de mexer nele) para o EF perceber a alteracao.
        ResponsaveisPorCategoria = new Dictionary<Guid, Guid>(responsaveis);
    }

    /// <summary>Cancelar apaga a lista; uma compra já finalizada é histórico e não pode ser cancelada.</summary>
    public void GarantirCancelavel()
    {
        if (Finalizada) throw new ErroDeDominio("Só é possível cancelar uma compra em andamento.");
    }

    public void Finalizar(DateTime agora, decimal totalGasto)
    {
        GarantirEditavel();
        Status = StatusDaListaEnum.Finalizada;
        FinalizadaEm = agora;
        TotalGastoFinal = totalGasto;
    }

    public void Reabrir()
    {
        if (!Finalizada) throw new ErroDeDominio("A lista já está em andamento.");
        Status = StatusDaListaEnum.EmAndamento;
        FinalizadaEm = null;
        TotalGastoFinal = null;
    }
}
