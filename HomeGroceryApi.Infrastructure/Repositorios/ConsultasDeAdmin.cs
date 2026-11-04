using System.Globalization;
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HomeGroceryApi.Infrastructure.Repositorios;

/// <summary>
/// Agregações de todo o app para o painel de administração. Só devolve contagens e somas (e a lista de contas para o
/// dono do app); nunca o conteúdo das listas das famílias.
/// </summary>
public sealed class ConsultasDeAdmin : IConsultasDeAdmin
{
    // Brasília não tem horário de verão desde 2019, então um deslocamento fixo basta para "o dia" do gráfico.
    private static readonly TimeSpan FusoDeBrasilia = TimeSpan.FromHours(-3);

    private readonly HomeGroceryDbContext _db;

    public ConsultasDeAdmin(HomeGroceryDbContext db)
    {
        _db = db;
    }

    public async Task<AdminResumoResposta> ResumoAsync(DateTime agora, CancellationToken ct)
    {
        var h24 = agora.AddHours(-24);
        var d7 = agora.AddDays(-7);
        var d30 = agora.AddDays(-30);

        var usuarios = new AdminUsuariosResumo(
            await _db.Users.CountAsync(ct),
            await _db.Users.CountAsync(u => u.CriadoEm >= d7, ct),
            await _db.Users.CountAsync(u => u.CriadoEm >= d30, ct),
            await _db.Users.CountAsync(u => u.UltimoAcessoEm >= h24, ct),
            await _db.Users.CountAsync(u => u.UltimoAcessoEm >= d7, ct),
            await _db.Users.CountAsync(u => u.UltimoAcessoEm >= d30, ct));

        // Família = grupo compartilhado com 2 ou mais pessoas ativas (o grupo pessoal de cada conta não conta).
        var grupos = new AdminGruposResumo(
            await _db.Grupos.CountAsync(ct),
            await _db.Grupos.CountAsync(g => !g.Pessoal && _db.Membros.Count(m => m.GrupoId == g.Id && m.Ativo) >= 2, ct));

        var finalizadas = await _db.Listas.CountAsync(l => l.Status == StatusDaListaEnum.Finalizada, ct);
        var listas = new AdminListasResumo(
            await _db.Listas.CountAsync(ct),
            await _db.Listas.CountAsync(l => l.Status == StatusDaListaEnum.EmAndamento, ct),
            finalizadas,
            await _db.Listas.Where(l => l.Status == StatusDaListaEnum.Finalizada).SumAsync(l => l.TotalGastoFinal ?? 0m, ct));

        var itens = new AdminItensResumo(await _db.ItensDasListas.CountAsync(ct), await _db.ItensDasListas.CountAsync(i => i.Comprado, ct));

        return new AdminResumoResposta(
            agora,
            usuarios,
            grupos,
            listas,
            itens,
            await _db.Lojas.CountAsync(l => l.ExcluidoEm == null, ct),
            await _db.NotasImportadas.CountAsync(ct),
            await _db.AssinaturasDePush.CountAsync(ct));
    }

    public async Task<AdminSeriesResposta> SeriesAsync(DateTime agora, int dias, CancellationToken ct)
    {
        var hoje = agora.Add(FusoDeBrasilia).Date;
        var primeiroDia = hoje.AddDays(-(dias - 1));
        var inicioUtc = DateTime.SpecifyKind(primeiroDia - FusoDeBrasilia, DateTimeKind.Utc);

        // Volume pequeno (uma janela de no máximo 90 dias): busca só os horários e agrupa por dia aqui, que é mais simples
        // do que traduzir fuso e truncamento de data para SQL.
        var cadastros = await _db.Users.Where(u => u.CriadoEm >= inicioUtc).Select(u => u.CriadoEm).ToListAsync(ct);
        var listas = await _db.Listas.Where(l => l.CriadoEm >= inicioUtc).Select(l => l.CriadoEm).ToListAsync(ct);
        var compras = await _db.ItensDasListas.Where(i => i.Comprado && i.CompradoEm >= inicioUtc).Select(i => i.CompradoEm!.Value).ToListAsync(ct);

        return new AdminSeriesResposta(dias, PorDia(cadastros, primeiroDia, dias), PorDia(listas, primeiroDia, dias), PorDia(compras, primeiroDia, dias));
    }

    public async Task<AdminUsuariosResposta> UsuariosAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var total = await _db.Users.CountAsync(ct);

        // Quem nunca voltou usa a data de criação; sem isso o Postgres põe os valores nulos primeiro na ordem decrescente.
        var contas = await _db.Users
            .OrderByDescending(u => u.UltimoAcessoEm ?? u.CriadoEm)
            .ThenBy(u => u.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(u => new { u.Id, u.Nome, Email = u.Email ?? string.Empty, u.CriadoEm, u.UltimoAcessoEm })
            .ToListAsync(ct);

        var ids = contas.Select(c => c.Id).ToList();

        var gruposAtivos = await (from m in _db.Membros
                                  join g in _db.Grupos on m.GrupoId equals g.Id
                                  where m.Ativo && ids.Contains(m.UsuarioId)
                                  select new { m.UsuarioId, g.Id, g.Nome }).ToListAsync(ct);

        var idsDosGrupos = gruposAtivos.Select(g => g.Id).Distinct().ToList();
        var membrosPorGrupo = await _db.Membros
            .Where(m => m.Ativo && idsDosGrupos.Contains(m.GrupoId))
            .GroupBy(m => m.GrupoId)
            .Select(g => new { GrupoId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.GrupoId, g => g.Quantidade, ct);

        var listasPorUsuario = await _db.Listas
            .Where(l => ids.Contains(l.CriadoPorUsuarioId))
            .GroupBy(l => l.CriadoPorUsuarioId)
            .Select(g => new { UsuarioId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.UsuarioId, g => g.Quantidade, ct);

        var comprasPorUsuario = await _db.ItensDasListas
            .Where(i => i.Comprado && i.CompradoPorUsuarioId != null && ids.Contains(i.CompradoPorUsuarioId.Value))
            .GroupBy(i => i.CompradoPorUsuarioId!.Value)
            .Select(g => new { UsuarioId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.UsuarioId, g => g.Quantidade, ct);

        var itens = contas.Select(c =>
        {
            var grupo = gruposAtivos.FirstOrDefault(g => g.UsuarioId == c.Id);
            return new AdminUsuarioResposta(
                c.Id,
                c.Nome,
                c.Email,
                c.CriadoEm,
                c.UltimoAcessoEm,
                grupo?.Nome ?? string.Empty,
                grupo is null ? 0 : membrosPorGrupo.GetValueOrDefault(grupo.Id),
                listasPorUsuario.GetValueOrDefault(c.Id),
                comprasPorUsuario.GetValueOrDefault(c.Id));
        }).ToList();

        return new AdminUsuariosResposta(total, pagina, tamanho, itens);
    }

    /// <summary>Conta quantos horários caem em cada dia de Brasília, devolvendo também os dias sem nada (valor 0).</summary>
    private static List<AdminPontoDoDia> PorDia(IEnumerable<DateTime> horariosUtc, DateTime primeiroDia, int dias)
    {
        var porDia = horariosUtc.GroupBy(h => h.Add(FusoDeBrasilia).Date).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, dias)
            .Select(i => primeiroDia.AddDays(i))
            .Select(dia => new AdminPontoDoDia(dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), porDia.GetValueOrDefault(dia)))
            .ToList();
    }
}
