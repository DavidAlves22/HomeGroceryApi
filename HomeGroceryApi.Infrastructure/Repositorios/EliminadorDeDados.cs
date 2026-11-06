using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

/// <summary>
/// Exclusão de verdade, em lote (ExecuteDelete: um DELETE por tabela, sem carregar linhas). As chaves estrangeiras são
/// Restrict, então a ordem importa: sempre do que depende para o que é dependido. Não passa pelo ChangeTracker, logo não
/// dispara avisos em tempo real por linha apagada; quem chama avisa o que interessa.
/// </summary>
public sealed class EliminadorDeDados : IEliminadorDeDados
{
    private readonly HomeGroceryDbContext _db;

    public EliminadorDeDados(HomeGroceryDbContext db)
    {
        _db = db;
    }

    public async Task ApagarContaAsync(PlanoDeExclusaoDeConta plano, CancellationToken ct)
    {
        await using var transacao = await _db.Database.BeginTransactionAsync(ct);

        foreach (var grupoId in plano.GruposParaApagar) await ApagarGrupoAsync(grupoId, ct);

        foreach (var saida in plano.GruposParaSair)
        {
            if (saida.NovoAdministradorId is { } novo)
            {
                await _db.Membros.Where(m => m.GrupoId == saida.GrupoId && m.UsuarioId == novo)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Papel, PapelNoGrupoEnum.Administrador), ct);
            }

            // Convites que a pessoa gerou e ninguém usou deixam de valer: o código dela não pode entrar em casa alheia depois.
            await _db.Convites.Where(c => c.GrupoId == saida.GrupoId && c.CriadoPorUsuarioId == plano.UsuarioId && c.UsadoPorUsuarioId == null).ExecuteDeleteAsync(ct);
            await _db.Membros.Where(m => m.GrupoId == saida.GrupoId && m.UsuarioId == plano.UsuarioId).ExecuteDeleteAsync(ct);
        }

        await _db.TokensDeRenovacao.Where(t => t.UsuarioId == plano.UsuarioId).ExecuteDeleteAsync(ct);
        await _db.CodigosDeRecuperacao.Where(c => c.UsuarioId == plano.UsuarioId).ExecuteDeleteAsync(ct);
        await _db.AssinaturasDePush.Where(a => a.UsuarioId == plano.UsuarioId).ExecuteDeleteAsync(ct);
        await _db.MensagensDeContato.Where(m => m.UsuarioId == plano.UsuarioId).ExecuteDeleteAsync(ct);

        // As tabelas do Identity (claims, logins, tokens, papéis) saem em cascata junto com o usuário.
        await _db.Users.Where(u => u.Id == plano.UsuarioId).ExecuteDeleteAsync(ct);

        await transacao.CommitAsync(ct);
    }

    public async Task ApagarMapaAsync(Guid lojaId, CancellationToken ct)
    {
        await using var transacao = await _db.Database.BeginTransactionAsync(ct);

        await ApagarDadosDoMapaAsync(lojaId, ct);

        await transacao.CommitAsync(ct);
    }

    private async Task ApagarGrupoAsync(Guid grupoId, CancellationToken ct)
    {
        await _db.LocaisDoProduto.Where(l => l.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.SecaoCategorias.Where(v => v.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Secoes.Where(s => s.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.ItensDasListas.Where(i => i.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.NotasImportadas.Where(n => n.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.VinculosDaNota.Where(v => v.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Listas.Where(l => l.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.ItensDoModelo.Where(i => i.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Produtos.Where(p => p.GrupoId == grupoId).ExecuteDeleteAsync(ct);

        // Cópias de mapas deste grupo em outros grupos guardam só o id de origem (sem chave estrangeira): soltam o vínculo.
        var lojas = _db.Lojas.Where(l => l.GrupoId == grupoId).Select(l => l.Id);
        await _db.Lojas.Where(l => l.OrigemId != null && lojas.Contains(l.OrigemId!.Value)).ExecuteUpdateAsync(s => s.SetProperty(l => l.OrigemId, (Guid?)null), ct);
        await _db.Lojas.Where(l => l.GrupoId == grupoId).ExecuteDeleteAsync(ct);

        await _db.Categorias.Where(c => c.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Convites.Where(c => c.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Membros.Where(m => m.GrupoId == grupoId).ExecuteDeleteAsync(ct);
        await _db.Grupos.Where(g => g.Id == grupoId).ExecuteDeleteAsync(ct);
    }

    private async Task ApagarDadosDoMapaAsync(Guid lojaId, CancellationToken ct)
    {
        // Compras antigas guardam em que mercado foram feitas: ficam, só sem o mercado (a tela diz "outro mercado").
        await _db.ItensDasListas.Where(i => i.CompradoNaLojaId == lojaId).ExecuteUpdateAsync(s => s.SetProperty(i => i.CompradoNaLojaId, (Guid?)null), ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE itens_da_lista SET \"LojasSemOItem\" = array_remove(\"LojasSemOItem\", {lojaId}) WHERE {lojaId} = ANY(\"LojasSemOItem\")", ct);

        await _db.LocaisDoProduto.Where(l => l.LojaId == lojaId).ExecuteDeleteAsync(ct);
        await _db.SecaoCategorias.Where(v => v.LojaId == lojaId).ExecuteDeleteAsync(ct);
        await _db.Secoes.Where(s => s.LojaId == lojaId).ExecuteDeleteAsync(ct);
        await _db.Lojas.Where(l => l.OrigemId == lojaId).ExecuteUpdateAsync(s => s.SetProperty(l => l.OrigemId, (Guid?)null), ct);
        await _db.Lojas.Where(l => l.Id == lojaId).ExecuteDeleteAsync(ct);
    }
}
