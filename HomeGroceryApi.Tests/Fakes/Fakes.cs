using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Tests.Fakes;

public sealed class RelogioFixo : IRelogio
{
    public DateTime AgoraUtc { get; set; } = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
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

    public Task<Guid?> ObterGrupoPessoalDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var grupoId = Membros.Where(m => m.UsuarioId == usuarioId).Select(m => (Guid?)m.GrupoId).FirstOrDefault();
        return Task.FromResult(grupoId);
    }
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
