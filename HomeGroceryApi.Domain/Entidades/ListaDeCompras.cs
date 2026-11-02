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

    public void Finalizar(DateTime agora)
    {
        GarantirEditavel();
        Status = StatusDaListaEnum.Finalizada;
        FinalizadaEm = agora;
    }

    public void Reabrir()
    {
        if (!Finalizada) throw new ErroDeDominio("A lista já está em andamento.");
        Status = StatusDaListaEnum.EmAndamento;
        FinalizadaEm = null;
    }
}
