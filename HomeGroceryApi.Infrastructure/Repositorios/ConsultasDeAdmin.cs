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

    public async Task<AdminInsightsResposta> InsightsAsync(DateTime agora, CancellationToken ct)
    {
        var totalDeContas = await _db.Users.CountAsync(ct);

        // "Voltou" = ainda abria o app pelo menos N dias depois de criar a conta. Contas antigas (de antes do painel)
        // não têm último acesso e contam como "não voltaram" até entrarem de novo.
        var retencao = new List<AdminRetencaoLinha>();
        foreach (var dias in new[] { 1, 7, 30 })
        {
            var limite = agora.AddDays(-dias);
            var elegiveis = await _db.Users.CountAsync(u => u.CriadoEm <= limite, ct);
            var voltaram = await _db.Users.CountAsync(u => u.CriadoEm <= limite && u.UltimoAcessoEm >= u.CriadoEm.AddDays(dias), ct);
            retencao.Add(new AdminRetencaoLinha(dias, elegiveis, voltaram));
        }

        // Os conjuntos de pessoas são pequenos (uma linha por conta), então é mais simples juntá-los aqui.
        var criaramLista = (await _db.Listas.Select(l => l.CriadoPorUsuarioId).Distinct().ToListAsync(ct)).ToHashSet();
        var compraram = (await _db.ItensDasListas.Where(i => i.Comprado && i.CompradoPorUsuarioId != null).Select(i => i.CompradoPorUsuarioId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        var finalizaram = (await _db.Listas.Where(l => l.Status == StatusDaListaEnum.Finalizada).Select(l => l.CriadoPorUsuarioId).Distinct().ToListAsync(ct)).ToHashSet();
        var comAtividade = criaramLista.Union(compraram).Count();
        var funil = new AdminFunil(totalDeContas, criaramLista.Count, compraram.Count, finalizaram.Count, Math.Max(totalDeContas - comAtividade, 0));

        var comGasto = _db.Listas.Where(l => l.Status == StatusDaListaEnum.Finalizada && l.TotalGastoFinal > 0);
        var comprasComGasto = await comGasto.CountAsync(ct);
        var medio = comprasComGasto == 0 ? 0m : await comGasto.AverageAsync(l => l.TotalGastoFinal ?? 0m, ct);
        var porMes = await comGasto
            .GroupBy(l => l.Competencia)
            .Select(g => new { Competencia = g.Key, Total = g.Sum(l => l.TotalGastoFinal ?? 0m), Compras = g.Count() })
            .OrderByDescending(m => m.Competencia)
            .Take(6)
            .ToListAsync(ct);
        var gasto = new AdminGasto(comprasComGasto, Math.Round(medio, 2), porMes.OrderBy(m => m.Competencia).Select(m => new AdminGastoDoMes(m.Competencia, m.Total, m.Compras)).ToList());

        var produtos = await _db.ItensDasListas
            .Where(i => i.Comprado)
            .GroupBy(i => i.NomeSnapshot.ToLower())
            .Select(g => new { Nome = g.Key, Compras = g.Count(), Casas = g.Select(i => i.GrupoId).Distinct().Count() })
            .OrderByDescending(p => p.Compras).ThenBy(p => p.Nome)
            .Take(10)
            .ToListAsync(ct);

        var categorias = await _db.ItensDasListas
            .Where(i => i.Comprado)
            .GroupBy(i => i.CategoriaNomeSnapshot)
            .Select(g => new { Nome = g.Key, Compras = g.Count() })
            .OrderByDescending(c => c.Compras).ThenBy(c => c.Nome)
            .Take(8)
            .ToListAsync(ct);

        var cidades = await _db.Lojas
            .Where(l => l.ExcluidoEm == null && l.CidadeBusca != null)
            .GroupBy(l => l.CidadeBusca)
            .Select(g => new { Cidade = g.Max(l => l.Cidade), Mercados = g.Count() })
            .OrderByDescending(c => c.Mercados).ThenBy(c => c.Cidade)
            .Take(8)
            .ToListAsync(ct);

        var copias = await _db.Lojas
            .Where(l => l.OrigemId != null)
            .GroupBy(l => l.OrigemId!.Value)
            .Select(g => new { OrigemId = g.Key, Copias = g.Count() })
            .OrderByDescending(c => c.Copias)
            .Take(5)
            .ToListAsync(ct);
        var idsDasOrigens = copias.Select(c => c.OrigemId).ToList();
        var origens = await _db.Lojas.Where(l => idsDasOrigens.Contains(l.Id)).Select(l => new { l.Id, l.Nome, l.Cidade }).ToDictionaryAsync(l => l.Id, ct);

        // Divisão da compra fica num jsonb em texto dentro da lista; só interessa saber em quais grupos já foi usada.
        var gruposQueDividiram = (await _db.Listas.Select(l => new { l.GrupoId, l.ResponsaveisPorCategoria }).ToListAsync(ct))
            .Where(l => l.ResponsaveisPorCategoria.Count > 0)
            .Select(l => l.GrupoId)
            .Distinct()
            .Count();

        var totalDeGrupos = await _db.Grupos.CountAsync(ct);
        var familias = await _db.Grupos.CountAsync(g => !g.Pessoal && _db.Membros.Count(m => m.GrupoId == g.Id && m.Ativo) >= 2, ct);
        var adocao = new List<AdminAdocao>
        {
            new("familia", familias, totalDeGrupos, "grupos"),
            new("divisao", gruposQueDividiram, totalDeGrupos, "grupos"),
            new("mercado", await _db.Lojas.Where(l => l.ExcluidoEm == null).Select(l => l.GrupoId).Distinct().CountAsync(ct), totalDeGrupos, "grupos"),
            new("nota", await _db.NotasImportadas.Select(n => n.GrupoId).Distinct().CountAsync(ct), totalDeGrupos, "grupos"),
            new("avisos", await _db.AssinaturasDePush.Select(a => a.UsuarioId).Distinct().CountAsync(ct), totalDeContas, "contas"),
        };

        return new AdminInsightsResposta(
            retencao,
            funil,
            gasto,
            produtos.Select(p => new AdminItemDeRanking(Capitalizar(p.Nome), p.Compras, p.Casas)).ToList(),
            categorias.Select(c => new AdminItemDeRanking(c.Nome, c.Compras, null)).ToList(),
            cidades.Select(c => new AdminItemDeRanking(c.Cidade ?? string.Empty, c.Mercados, null)).ToList(),
            copias.Select(c => origens.TryGetValue(c.OrigemId, out var o) ? new AdminItemDeRanking(string.IsNullOrWhiteSpace(o.Cidade) ? o.Nome : $"{o.Nome} ({o.Cidade})", c.Copias, null) : null).OfType<AdminItemDeRanking>().ToList(),
            adocao);
    }

    private static string Capitalizar(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], CultureInfo.GetCultureInfo("pt-BR")) + texto[1..];

    public async Task<AdminUsuariosResposta> UsuariosAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var total = await _db.Users.CountAsync(ct);

        // Quem nunca voltou usa a data de criação; sem isso o Postgres põe os valores nulos primeiro na ordem decrescente.
        var contas = await _db.Users
            .OrderByDescending(u => u.UltimoAcessoEm ?? u.CriadoEm)
            .ThenBy(u => u.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(u => new { u.Id, u.Nome, Email = u.Email ?? string.Empty, u.CriadoEm, u.UltimoAcessoEm, u.BloqueadoEm })
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
                comprasPorUsuario.GetValueOrDefault(c.Id),
                c.BloqueadoEm);
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
