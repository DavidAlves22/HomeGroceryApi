using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Tests.Fakes;

public sealed class RelogioFixo : IRelogio
{
    public DateTime AgoraUtc { get; set; } = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class AdministradoresFake : IAdministradores
{
    public HashSet<string> Emails { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool EhAdministrador(string email) => Emails.Contains(email);
}

public sealed class TokenServiceFake : ITokenService
{
    private int _contador;

    public (string Token, DateTime ExpiraEm) GerarTokenDeAcesso(Guid usuarioId, string email, Guid grupoId)
    {
        return ($"acesso-{usuarioId}", DateTime.UtcNow.AddMinutes(30));
    }

    public (string Token, DateTime ExpiraEm) GerarTokenDeRenovacao()
    {
        return ($"renovacao-{++_contador}", new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    public string CalcularHash(string token)
    {
        return $"hash:{token}";
    }
}

public sealed class ServicoDeContasFake : IServicoDeContas
{
    private readonly Dictionary<string, (ContaDoUsuario Conta, string Senha)> _contas = new();

    public Task<ResultadoDeCriacaoDeConta> CriarAsync(string email, string nome, string senha, CancellationToken ct)
    {
        if (_contas.ContainsKey(email)) return Task.FromResult(new ResultadoDeCriacaoDeConta(null, true, []));

        var conta = new ContaDoUsuario(Guid.NewGuid(), nome, email);
        _contas[email] = (conta, senha);
        return Task.FromResult(new ResultadoDeCriacaoDeConta(conta.Id, false, []));
    }

    public Task<ContaDoUsuario?> ValidarCredenciaisAsync(string email, string senha, CancellationToken ct)
    {
        var achou = _contas.TryGetValue(email, out var registro) && registro.Senha == senha;
        return Task.FromResult(achou ? registro.Conta : null);
    }

    public Task<ContaDoUsuario?> ObterPorIdAsync(Guid usuarioId, CancellationToken ct)
    {
        return Task.FromResult(_contas.Values.Select(v => v.Conta).FirstOrDefault(c => c.Id == usuarioId));
    }

    public Task<ContaDoUsuario?> ObterPorEmailAsync(string email, CancellationToken ct)
    {
        return Task.FromResult(_contas.TryGetValue(email, out var registro) ? registro.Conta : null);
    }

    public Task<IReadOnlyList<string>> RedefinirSenhaAsync(Guid usuarioId, string novaSenha, CancellationToken ct)
    {
        var chave = _contas.Keys.First(k => _contas[k].Conta.Id == usuarioId);
        _contas[chave] = (_contas[chave].Conta, novaSenha);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public Task AlterarNomeAsync(Guid usuarioId, string nome, CancellationToken ct)
    {
        var chave = _contas.Keys.First(k => _contas[k].Conta.Id == usuarioId);
        _contas[chave] = (_contas[chave].Conta with { Nome = nome }, _contas[chave].Senha);
        return Task.CompletedTask;
    }

    public Task<bool> DefinirBloqueioAsync(Guid usuarioId, DateTime? bloqueadoEm, CancellationToken ct)
    {
        var chave = _contas.Keys.FirstOrDefault(k => _contas[k].Conta.Id == usuarioId);
        if (chave is null) return Task.FromResult(false);

        _contas[chave] = (_contas[chave].Conta with { Bloqueada = bloqueadoEm is not null }, _contas[chave].Senha);
        return Task.FromResult(true);
    }

    public Task<bool> EstaBloqueadaAsync(Guid usuarioId, CancellationToken ct)
    {
        var conta = _contas.Values.Select(v => v.Conta).FirstOrDefault(c => c.Id == usuarioId);
        return Task.FromResult(conta is null || conta.Bloqueada);
    }

    public Dictionary<Guid, DateTime> Acessos { get; } = new();

    public Task RegistrarAcessoAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        Acessos[usuarioId] = agora;
        return Task.CompletedTask;
    }
}

public sealed class GrupoRepositoryFake : IGrupoRepository
{
    public List<Grupo> Grupos { get; } = [];
    public List<MembroDoGrupo> Membros { get; } = [];
    public List<Categoria> Categorias { get; } = [];

    public Task AdicionarAsync(Grupo grupo, MembroDoGrupo membro, IEnumerable<Categoria> categorias, CancellationToken ct)
    {
        Grupos.Add(grupo);
        Membros.Add(membro);
        Categorias.AddRange(categorias);
        return Task.CompletedTask;
    }

    public Task<Guid?> ObterGrupoAtivoDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var grupoId = Membros.Where(m => m.UsuarioId == usuarioId && m.Ativo).Select(m => (Guid?)m.GrupoId).FirstOrDefault();
        return Task.FromResult(grupoId);
    }

    public Task<Grupo?> ObterAsync(Guid grupoId, CancellationToken ct) => Task.FromResult(Grupos.FirstOrDefault(g => g.Id == grupoId));

    public Task<bool> EhMembroAtivoAsync(Guid usuarioId, Guid grupoId, CancellationToken ct) => Task.FromResult(Membros.Any(m => m.UsuarioId == usuarioId && m.GrupoId == grupoId && m.Ativo));

    public Task<MembroDoGrupo?> ObterMembroAsync(Guid grupoId, Guid usuarioId, CancellationToken ct) => Task.FromResult(Membros.FirstOrDefault(m => m.GrupoId == grupoId && m.UsuarioId == usuarioId));

    public Task<List<MembroDoGrupo>> ListarMembrosAsync(Guid grupoId, CancellationToken ct) => Task.FromResult(Membros.Where(m => m.GrupoId == grupoId).ToList());

    public Task<List<MembroDoGrupo>> ListarVinculosDoUsuarioAsync(Guid usuarioId, CancellationToken ct) => Task.FromResult(Membros.Where(m => m.UsuarioId == usuarioId).ToList());

    public Task AdicionarMembroAsync(MembroDoGrupo membro, CancellationToken ct)
    {
        Membros.Add(membro);
        return Task.CompletedTask;
    }

    public void RemoverMembro(MembroDoGrupo membro) => Membros.Remove(membro);
}

public sealed class TokenDeRenovacaoRepositoryFake : ITokenDeRenovacaoRepository
{
    public List<TokenDeRenovacao> Tokens { get; } = [];

    public Task AdicionarAsync(TokenDeRenovacao token, CancellationToken ct)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<TokenDeRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct)
    {
        return Task.FromResult(Tokens.FirstOrDefault(t => t.Hash == hash));
    }

    public Task RevogarTodosDoUsuarioAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        foreach (var token in Tokens.Where(t => t.UsuarioId == usuarioId)) token.Revogar(agora);
        return Task.CompletedTask;
    }
}

public sealed class UnidadeDeTrabalhoFake : IUnidadeDeTrabalho
{
    public int Salvamentos { get; private set; }

    public Task SalvarAsync(CancellationToken ct)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }

    public Task ExecutarEmTransacaoAsync(Func<Task> acao, CancellationToken ct)
    {
        return acao();
    }
}
