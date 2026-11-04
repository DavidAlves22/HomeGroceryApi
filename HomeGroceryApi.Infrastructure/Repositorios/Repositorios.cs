using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeGroceryApi.Infrastructure.Repositorios;

public sealed class GrupoRepository : IGrupoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public GrupoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(Grupo grupo, MembroDoGrupo membro, IEnumerable<Categoria> categorias, CancellationToken ct)
    {
        await _contexto.Grupos.AddAsync(grupo, ct);
        await _contexto.Membros.AddAsync(membro, ct);
        await _contexto.Categorias.AddRangeAsync(categorias, ct);
    }

    public Task<Grupo?> ObterAsync(Guid grupoId, CancellationToken ct)
    {
        return _contexto.Grupos.FirstOrDefaultAsync(g => g.Id == grupoId, ct);
    }

    public Task<Guid?> ObterGrupoAtivoDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        return _contexto.Membros.Where(m => m.UsuarioId == usuarioId && m.Ativo).Select(m => (Guid?)m.GrupoId).FirstOrDefaultAsync(ct);
    }

    public Task<bool> EhMembroAtivoAsync(Guid usuarioId, Guid grupoId, CancellationToken ct)
    {
        return _contexto.Membros.AsNoTracking().AnyAsync(m => m.UsuarioId == usuarioId && m.GrupoId == grupoId && m.Ativo, ct);
    }

    public Task<MembroDoGrupo?> ObterMembroAsync(Guid grupoId, Guid usuarioId, CancellationToken ct)
    {
        return _contexto.Membros.FirstOrDefaultAsync(m => m.GrupoId == grupoId && m.UsuarioId == usuarioId, ct);
    }

    public Task<List<MembroDoGrupo>> ListarMembrosAsync(Guid grupoId, CancellationToken ct)
    {
        return _contexto.Membros.Where(m => m.GrupoId == grupoId).ToListAsync(ct);
    }

    public Task<List<MembroDoGrupo>> ListarVinculosDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        return _contexto.Membros.Where(m => m.UsuarioId == usuarioId).ToListAsync(ct);
    }

    public async Task AdicionarMembroAsync(MembroDoGrupo membro, CancellationToken ct)
    {
        await _contexto.Membros.AddAsync(membro, ct);
    }

    public void RemoverMembro(MembroDoGrupo membro)
    {
        _contexto.Membros.Remove(membro);
    }
}

public sealed class ConviteRepository : IConviteRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public ConviteRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(Convite convite, CancellationToken ct)
    {
        await _contexto.Convites.AddAsync(convite, ct);
    }

    public Task<Convite?> ObterPorCodigoAsync(string codigoNormalizado, CancellationToken ct)
    {
        return _contexto.Convites.FirstOrDefaultAsync(c => c.Codigo == codigoNormalizado, ct);
    }
}

public sealed class TokenDeRenovacaoRepository : ITokenDeRenovacaoRepository
{
    private readonly HomeGroceryDbContext _contexto;

    public TokenDeRenovacaoRepository(HomeGroceryDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(TokenDeRenovacao token, CancellationToken ct)
    {
        await _contexto.TokensDeRenovacao.AddAsync(token, ct);
    }

    public Task<TokenDeRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct)
    {
        return _contexto.TokensDeRenovacao.FirstOrDefaultAsync(t => t.Hash == hash, ct);
    }
}

/// <summary>
/// Confirma as alterações e, DEPOIS de gravadas, avisa os outros aparelhos do grupo. Descobrir o que mudou
/// aqui (olhando o que o EF vai gravar) evita que cada caso de uso precise lembrar de notificar: uma
/// operação nova que grave listas, produtos ou membros já avisa sozinha.
/// </summary>
public sealed class UnidadeDeTrabalho : IUnidadeDeTrabalho
{
    private readonly HomeGroceryDbContext _contexto;
    private readonly INotificador _notificador;
    private readonly INotificadorDePush _push;
    private readonly IUsuarioAtual _usuario;
    private readonly ILogger<UnidadeDeTrabalho> _log;

    public UnidadeDeTrabalho(HomeGroceryDbContext contexto, INotificador notificador, INotificadorDePush push, IUsuarioAtual usuario, ILogger<UnidadeDeTrabalho> log)
    {
        _contexto = contexto;
        _notificador = notificador;
        _push = push;
        _usuario = usuario;
        _log = log;
    }

    public async Task SalvarAsync(CancellationToken ct)
    {
        var avisos = ColetarAvisos();
        var avisosPush = _push.Habilitado ? ColetarAvisosPush() : [];
        await _contexto.SaveChangesAsync(ct);
        await PublicarAsync(avisos, ct);
        await EnviarPushAsync(avisosPush, ct);
    }

    /// <summary>O que gera aviso no celular dos outros: item novo pedido por alguém (fora da criação da lista) e compra finalizada.</summary>
    private sealed record AvisoPush(Guid GrupoId, Guid? AutorId, bool Finalizada, IReadOnlyList<string> Itens, string? Competencia, Guid ListaId);

    private List<AvisoPush> ColetarAvisosPush()
    {
        var entradas = _contexto.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList();
        var listasNovas = entradas.Where(e => e.State == EntityState.Added).Select(e => e.Entity).OfType<ListaDeCompras>().Select(l => l.Id).ToHashSet();
        var avisos = new List<AvisoPush>();

        // Itens acrescentados a uma lista que já existia (criar a lista com 30 itens não avisa 30 vezes).
        var itensNovos = entradas.Where(e => e.State == EntityState.Added).Select(e => e.Entity).OfType<ItemDaLista>().Where(i => !listasNovas.Contains(i.ListaId));
        foreach (var grupo in itensNovos.GroupBy(i => (i.GrupoId, i.ListaId, i.AdicionadoPorUsuarioId)))
        {
            avisos.Add(new AvisoPush(grupo.Key.GrupoId, grupo.Key.AdicionadoPorUsuarioId, false, grupo.Select(i => i.NomeSnapshot).ToList(), null, grupo.Key.ListaId));
        }

        foreach (var entrada in entradas.Where(e => e.State == EntityState.Modified && e.Entity is ListaDeCompras))
        {
            var status = entrada.Property(nameof(ListaDeCompras.Status));
            var lista = (ListaDeCompras)entrada.Entity;
            if (!status.IsModified || (StatusDaListaEnum)status.OriginalValue! == StatusDaListaEnum.Finalizada || !lista.Finalizada) continue;

            Guid? autor = null;
            try
            {
                autor = _usuario.UsuarioId;
            }
            catch
            {
                // Sem usuário na requisição: o aviso sai sem nome.
            }

            avisos.Add(new AvisoPush(lista.GrupoId, autor, true, [], lista.Competencia, lista.Id));
        }

        return avisos;
    }

    private async Task EnviarPushAsync(List<AvisoPush> avisos, CancellationToken ct)
    {
        foreach (var aviso in avisos)
        {
            try
            {
                var destinatarios = await _contexto.Membros.Where(m => m.GrupoId == aviso.GrupoId && m.Ativo && m.UsuarioId != aviso.AutorId).Select(m => m.UsuarioId).ToListAsync(ct);
                if (destinatarios.Count == 0) continue;

                var nome = aviso.AutorId is { } autor ? (await _contexto.Users.Where(u => u.Id == autor).Select(u => u.Nome).FirstOrDefaultAsync(ct))?.Split(' ')[0] : null;
                nome = string.IsNullOrWhiteSpace(nome) ? "Alguém" : nome;

                NotificacaoDePush notificacao;
                if (aviso.Finalizada)
                {
                    notificacao = new NotificacaoDePush("Compra finalizada", $"{nome} finalizou a compra de {Competencia.Parse(aviso.Competencia!).Nome}.", "/lista", $"finalizada-{aviso.ListaId:N}");
                }
                else
                {
                    var titulo = aviso.Itens.Count == 1 ? $"{nome} pediu um item" : $"{nome} adicionou {aviso.Itens.Count} itens";
                    var corpo = string.Join(", ", aviso.Itens.Take(3)) + (aviso.Itens.Count > 3 ? $" e mais {aviso.Itens.Count - 3}" : string.Empty);
                    notificacao = new NotificacaoDePush(titulo, corpo, "/lista", $"pedido-{aviso.ListaId:N}");
                }

                await _push.EnviarAsync(destinatarios, notificacao, ct);
            }
            catch (Exception erro)
            {
                // O dado já está gravado: um aviso que falha nunca desfaz nem derruba a operação.
                _log.LogWarning(erro, "Falha ao enviar aviso por push");
            }
        }
    }

    public async Task ExecutarEmTransacaoAsync(Func<Task> acao, CancellationToken ct)
    {
        await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);
        await acao();
        await transacao.CommitAsync(ct);
    }

    private sealed record Aviso(Guid? GrupoId, Guid? UsuarioId, EventoDeAlteracao Evento);

    private List<Aviso> ColetarAvisos()
    {
        var mudancas = _contexto.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        var avisos = new List<Aviso>();

        // Itens e listas: um aviso por lista (com o id do item só quando foi exatamente um).
        var itens = mudancas.Select(e => e.Entity).OfType<ItemDaLista>().ToList();
        var listas = mudancas.Select(e => e.Entity).OfType<ListaDeCompras>().ToList();
        foreach (var grupo in itens.GroupBy(i => (i.GrupoId, i.ListaId)))
        {
            avisos.Add(new Aviso(grupo.Key.GrupoId, null, new EventoDeAlteracao(EventoDeAlteracao.Lista, grupo.Key.ListaId, grupo.Count() == 1 ? grupo.First().Id : null)));
        }

        foreach (var lista in listas.Where(l => itens.All(i => i.ListaId != l.Id)))
        {
            avisos.Add(new Aviso(lista.GrupoId, null, new EventoDeAlteracao(EventoDeAlteracao.Lista, lista.Id)));
        }

        // Lista padrão, categorias e produtos: o cadastro do grupo mudou.
        var gruposDoCatalogo = mudancas.Select(e => e.Entity).Select(e => e switch
        {
            ItemDoModelo i => (Guid?)i.GrupoId,
            Categoria c => c.GrupoId,
            Produto p => p.GrupoId,
            _ => null,
        }).Where(g => g.HasValue).Select(g => g!.Value).Distinct();
        foreach (var grupoId in gruposDoCatalogo) avisos.Add(new Aviso(grupoId, null, new EventoDeAlteracao(EventoDeAlteracao.Catalogo)));

        // Mercados e mapas: o desenho de um mercado mudou (um aviso por grupo, mesmo que muitas seções tenham sido regravadas).
        var gruposDosMapas = mudancas.Select(e => e.Entity).Select(e => e switch
        {
            Loja l => (Guid?)l.GrupoId,
            Secao s => s.GrupoId,
            LocalDoProduto local => local.GrupoId,
            _ => null,
        }).Where(g => g.HasValue).Select(g => g!.Value).Distinct();
        foreach (var grupoId in gruposDosMapas) avisos.Add(new Aviso(grupoId, null, new EventoDeAlteracao(EventoDeAlteracao.Lojas)));

        // Membros e nome do grupo: avisa o grupo e a própria pessoa (que pode estar em outro aparelho).
        foreach (var membro in mudancas.Select(e => e.Entity).OfType<MembroDoGrupo>())
        {
            avisos.Add(new Aviso(membro.GrupoId, null, new EventoDeAlteracao(EventoDeAlteracao.Membros)));
            avisos.Add(new Aviso(null, membro.UsuarioId, new EventoDeAlteracao(EventoDeAlteracao.GrupoAlterado)));
        }

        foreach (var grupo in mudancas.Select(e => e.Entity).OfType<Grupo>()) avisos.Add(new Aviso(grupo.Id, null, new EventoDeAlteracao(EventoDeAlteracao.Membros)));

        return avisos;
    }

    private async Task PublicarAsync(List<Aviso> avisos, CancellationToken ct)
    {
        foreach (var aviso in avisos)
        {
            try
            {
                if (aviso.GrupoId is { } grupoId) await _notificador.PublicarNoGrupoAsync(grupoId, aviso.Evento, ct);
                if (aviso.UsuarioId is { } usuarioId) await _notificador.PublicarParaUsuarioAsync(usuarioId, aviso.Evento, ct);
            }
            catch (Exception erro)
            {
                // O dado já está gravado: falhar em avisar não pode desfazer nem derrubar a operação.
                _log.LogWarning(erro, "Falha ao publicar evento {Tipo}", aviso.Evento.Tipo);
            }
        }
    }
}
