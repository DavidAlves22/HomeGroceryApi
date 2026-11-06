namespace HomeGroceryApi.Application.Interfaces;

/// <summary>Um grupo compartilhado que a pessoa deixa: quem fica, e quem vira administrador se ela era a única.</summary>
public sealed record SaidaDeGrupo(Guid GrupoId, Guid? NovoAdministradorId);

/// <param name="GruposParaApagar">Grupos em que a pessoa é o único vínculo: somem inteiros, com tudo o que têm.</param>
/// <param name="GruposParaSair">Grupos com mais gente: a pessoa sai e os dados continuam com os outros.</param>
public sealed record PlanoDeExclusaoDeConta(Guid UsuarioId, IReadOnlyList<Guid> GruposParaApagar, IReadOnlyList<SaidaDeGrupo> GruposParaSair);

/// <summary>
/// Apaga de verdade (DELETE no banco, não exclusão lógica) o que o encerramento de conta e a moderação de mapas exigem.
/// Cada operação roda numa transação só: ou apaga tudo ou nada. Quem implementa conhece a ordem das chaves estrangeiras.
/// </summary>
public interface IEliminadorDeDados
{
    Task ApagarContaAsync(PlanoDeExclusaoDeConta plano, CancellationToken ct);

    /// <summary>Remove o mapa (seções, categorias das seções, locais de produto). Compras antigas ficam, só sem o mercado.</summary>
    Task ApagarMapaAsync(Guid lojaId, CancellationToken ct);
}
