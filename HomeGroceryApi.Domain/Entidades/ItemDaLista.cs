using HomeGroceryApi.Domain.Enums;

namespace HomeGroceryApi.Domain.Entidades;

/// <summary>
/// Item de uma lista mensal. Guarda um SNAPSHOT (nome, categoria e unidade copiados) para que
/// editar ou apagar o cadastro depois nunca altere o histórico. `Versao` sobe a cada alteração e
/// serve de controle de concorrência: duas pessoas editando o mesmo item não se sobrescrevem.
/// Os preços também ficam no item: o valor pago neste mês nunca muda por causa de compras futuras.
/// </summary>
public sealed class ItemDaLista
{
    public Guid Id { get; private set; }
    public Guid ListaId { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public string NomeSnapshot { get; private set; } = string.Empty;
    public Guid? CategoriaId { get; private set; }
    public string CategoriaNomeSnapshot { get; private set; } = string.Empty;
    public UnidadeDeMedidaEnum UnidadeSnapshot { get; private set; }
    public decimal QuantidadePlanejada { get; private set; }

    /// <summary>Quantidade que realmente foi para o carrinho. Vazia = igual à planejada.</summary>
    public decimal? QuantidadeComprada { get; private set; }

    /// <summary>Preço unitário esperado (referência para o total estimado).</summary>
    public decimal? PrecoEstimado { get; private set; }

    /// <summary>Preço unitário efetivamente pago. Só ele entra no total gasto.</summary>
    public decimal? PrecoUnitario { get; private set; }

    /// <summary>Preço pago em outra lista (ao copiar um mês). Só consulta: nunca entra em totais.</summary>
    public decimal? PrecoReferencia { get; private set; }

    public bool Comprado { get; private set; }

    /// <summary>Mercado onde foi comprado. A compra do mês pode ser dividida entre vários mercados.</summary>
    public Guid? CompradoNaLojaId { get; private set; }

    /// <summary>Mercados que não têm este item ("Não tem aqui"). Ele continua pendente e entra na rota do próximo mercado.</summary>
    public Guid[] LojasSemOItem { get; private set; } = [];

    public DateTime? CompradoEm { get; private set; }
    public Guid? CompradoPorUsuarioId { get; private set; }
    public Guid AdicionadoPorUsuarioId { get; private set; }
    public int Ordem { get; private set; }
    public int Versao { get; private set; }

    private ItemDaLista()
    {
    }

    public static ItemDaLista Criar(Guid id, Guid listaId, Guid grupoId, Guid? produtoId, string nome, Guid? categoriaId, string categoriaNome, UnidadeDeMedidaEnum unidade, decimal quantidade, int ordem, Guid usuarioId, decimal? precoEstimado = null, decimal? precoReferencia = null)
    {
        return new ItemDaLista
        {
            Id = id, ListaId = listaId, GrupoId = grupoId, ProdutoId = produtoId, NomeSnapshot = nome, CategoriaId = categoriaId,
            CategoriaNomeSnapshot = categoriaNome, UnidadeSnapshot = unidade, QuantidadePlanejada = quantidade, Ordem = ordem,
            AdicionadoPorUsuarioId = usuarioId, PrecoEstimado = precoEstimado, PrecoReferencia = precoReferencia, Versao = 1,
        };
    }

    public void Editar(Guid produtoId, string nome, Guid categoriaId, string categoriaNome, UnidadeDeMedidaEnum unidade, decimal quantidade, decimal? quantidadeComprada, decimal? precoEstimado, decimal? precoUnitario)
    {
        ProdutoId = produtoId;
        NomeSnapshot = nome;
        CategoriaId = categoriaId;
        CategoriaNomeSnapshot = categoriaNome;
        UnidadeSnapshot = unidade;
        QuantidadePlanejada = quantidade;
        QuantidadeComprada = quantidadeComprada;
        PrecoEstimado = precoEstimado;
        PrecoUnitario = precoUnitario;
        Versao++;
    }

    public void DefinirOrdem(int ordem)
    {
        Ordem = ordem;
    }

    /// <summary>
    /// Marcar/desmarcar é idempotente (repetir o mesmo comando dá o mesmo resultado) e não exige versão:
    /// quem marcou por último vale, e o app mostra quem foi.
    /// </summary>
    public void DefinirComprado(bool comprado, Guid usuarioId, DateTime agora, Guid? lojaId = null)
    {
        if (Comprado == comprado)
        {
            // Já comprado: só muda se agora se informou OUTRO mercado (corrigir onde foi comprado).
            if (comprado && lojaId is not null && CompradoNaLojaId != lojaId)
            {
                CompradoNaLojaId = lojaId;
                Versao++;
            }

            return;
        }

        Comprado = comprado;
        CompradoEm = comprado ? agora : null;
        CompradoPorUsuarioId = comprado ? usuarioId : null;
        CompradoNaLojaId = comprado ? lojaId : null;
        Versao++;
    }

    /// <summary>
    /// Compra vinda da nota fiscal: guarda a quantidade e o preço pagos e marca como comprado de uma vez.
    /// Diferente de marcar/desmarcar, sobrescreve preço e quantidade que já existissem (a nota é a fonte da verdade).
    /// A unidade da nota também manda: se for diferente da do item (Litro x Kg, por exemplo), o item passa a usá-la e a
    /// quantidade planejada e o preço estimado, que estavam na unidade antiga, são descartados (sem conversão: 1 L não é 1 kg).
    /// </summary>
    public void RegistrarCompra(decimal quantidadeComprada, decimal precoUnitario, Guid usuarioId, DateTime agora, Guid? lojaId, UnidadeDeMedidaEnum? unidadeDaNota = null)
    {
        if (unidadeDaNota is { } unidade && unidade != UnidadeSnapshot)
        {
            UnidadeSnapshot = unidade;
            QuantidadePlanejada = quantidadeComprada;
            PrecoEstimado = null;
        }

        QuantidadeComprada = quantidadeComprada;
        PrecoUnitario = precoUnitario;
        CompradoEm ??= agora;
        CompradoPorUsuarioId = usuarioId;
        CompradoNaLojaId = lojaId ?? CompradoNaLojaId;
        Comprado = true;
        Versao++;
    }

    public void DefinirSemNaLoja(Guid lojaId, bool sem)
    {
        if (LojasSemOItem.Contains(lojaId) == sem) return;

        // Troca o array inteiro (em vez de alterar o existente) para o EF perceber a mudança.
        LojasSemOItem = sem ? [.. LojasSemOItem, lojaId] : LojasSemOItem.Where(l => l != lojaId).ToArray();
        Versao++;
    }
}
