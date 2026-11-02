using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Autenticacao;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Grupos;

/// <summary>
/// Base dos casos de uso do grupo: lê o vínculo do usuário atual e aplica as regras de permissão
/// (só administrador convida, renomeia e remove) em um lugar só.
/// </summary>
public abstract class CasoDeUsoDeGrupo
{
    protected CasoDeUsoDeGrupo(IGrupoRepository grupos, IUsuarioAtual usuario)
    {
        Grupos = grupos;
        Usuario = usuario;
    }

    protected IGrupoRepository Grupos { get; }
    protected IUsuarioAtual Usuario { get; }

    protected async Task<MembroDoGrupo> ObterMeuVinculoAsync(CancellationToken ct)
    {
        return await Grupos.ObterMembroAsync(Usuario.GrupoId, Usuario.UsuarioId, ct) ?? throw new ErroDeAutenticacao("Você não faz parte deste grupo.");
    }

    protected async Task<MembroDoGrupo> ObterVinculoDeAdministradorAsync(CancellationToken ct)
    {
        var vinculo = await ObterMeuVinculoAsync(ct);
        if (!vinculo.Administrador) throw new ErroDeAcessoNegado("Só quem administra o grupo pode fazer isso.");
        return vinculo;
    }
}

public sealed class ObterMeuGrupo : CasoDeUsoDeGrupo
{
    private readonly IServicoDeContas _contas;

    public ObterMeuGrupo(IGrupoRepository grupos, IUsuarioAtual usuario, IServicoDeContas contas) : base(grupos, usuario)
    {
        _contas = contas;
    }

    public async Task<GrupoResposta> ExecutarAsync(CancellationToken ct)
    {
        var meu = await ObterMeuVinculoAsync(ct);
        var grupo = await Grupos.ObterAsync(Usuario.GrupoId, ct) ?? throw new ErroDeNaoEncontrado("Grupo não encontrado.");
        var vinculos = await Grupos.ListarMembrosAsync(grupo.Id, ct);

        var membros = new List<MembroResposta>();
        foreach (var vinculo in vinculos.OrderBy(v => v.EntrouEm))
        {
            var conta = await _contas.ObterPorIdAsync(vinculo.UsuarioId, ct);
            membros.Add(new MembroResposta(vinculo.UsuarioId, conta?.Nome ?? "Usuário", vinculo.Papel.ToString(), vinculo.UsuarioId == Usuario.UsuarioId, vinculo.EntrouEm));
        }

        return new GrupoResposta(grupo.Id, grupo.Nome, membros.Count > 1, meu.Papel.ToString(), membros);
    }
}

public sealed class RenomearGrupo : CasoDeUsoDeGrupo
{
    private readonly IUnidadeDeTrabalho _unidade;

    public RenomearGrupo(IGrupoRepository grupos, IUsuarioAtual usuario, IUnidadeDeTrabalho unidade) : base(grupos, usuario)
    {
        _unidade = unidade;
    }

    public async Task ExecutarAsync(NomeRequisicao requisicao, CancellationToken ct)
    {
        var nome = Entrada.Nome(requisicao.Nome, "O nome do grupo");
        await ObterVinculoDeAdministradorAsync(ct);
        var grupo = await Grupos.ObterAsync(Usuario.GrupoId, ct) ?? throw new ErroDeNaoEncontrado("Grupo não encontrado.");

        grupo.Renomear(nome);
        await _unidade.SalvarAsync(ct);
    }
}

/// <summary>
/// Gera o código que uma pessoa digita para entrar na família. Vale por 7 dias e uma única vez.
/// </summary>
public sealed class CriarConvite : CasoDeUsoDeGrupo
{
    private readonly IConviteRepository _convites;
    private readonly IGeradorDeCodigo _gerador;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public CriarConvite(IGrupoRepository grupos, IUsuarioAtual usuario, IConviteRepository convites, IGeradorDeCodigo gerador, IUnidadeDeTrabalho unidade, IRelogio relogio) : base(grupos, usuario)
    {
        _convites = convites;
        _gerador = gerador;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<ConviteResposta> ExecutarAsync(CancellationToken ct)
    {
        await ObterVinculoDeAdministradorAsync(ct);

        var convite = Convite.Criar(Guid.CreateVersion7(), Usuario.GrupoId, _gerador.Gerar(), Usuario.UsuarioId, _relogio.AgoraUtc);
        await _convites.AdicionarAsync(convite, ct);
        await _unidade.SalvarAsync(ct);

        return new ConviteResposta($"{convite.Codigo[..4]}-{convite.Codigo[4..]}", convite.ExpiraEm);
    }
}

/// <summary>
/// Entra em uma família com um código. Os dados pessoais de quem entra ficam guardados no grupo antigo
/// (não são misturados) e voltam se a pessoa sair. A resposta é uma sessão NOVA, já apontando para o grupo novo.
/// </summary>
public sealed class EntrarEmGrupo : CasoDeUsoDeGrupo
{
    private const string MensagemDeConviteInvalido = "Convite inválido ou expirado. Peça um novo código.";

    private readonly IConviteRepository _convites;
    private readonly IServicoDeContas _contas;
    private readonly EmissorDeSessao _emissor;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public EntrarEmGrupo(IGrupoRepository grupos, IUsuarioAtual usuario, IConviteRepository convites, IServicoDeContas contas, EmissorDeSessao emissor, IUnidadeDeTrabalho unidade, IRelogio relogio) : base(grupos, usuario)
    {
        _convites = convites;
        _contas = contas;
        _emissor = emissor;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<SessaoResposta> ExecutarAsync(EntrarNoGrupoRequisicao requisicao, CancellationToken ct)
    {
        var codigo = Convite.NormalizarCodigo(requisicao.Codigo);
        if (codigo.Length != 8) throw new ErroDeValidacao("O código tem 8 letras e números, como K7M2-XQ9P.");

        var agora = _relogio.AgoraUtc;
        // A mesma mensagem para "não existe", "expirou" e "já usado": quem chuta códigos não descobre quais existem.
        var convite = await _convites.ObterPorCodigoAsync(codigo, ct);
        if (convite is null || !convite.EstaValido(agora)) throw new ErroDeNaoEncontrado(MensagemDeConviteInvalido);

        if (convite.GrupoId == Usuario.GrupoId) throw new ErroDeConflito("Você já faz parte deste grupo.");
        if ((await Grupos.ListarMembrosAsync(Usuario.GrupoId, ct)).Count > 1) throw new ErroDeConflito("Saia do grupo atual antes de entrar em outro.");

        var conta = await _contas.ObterPorIdAsync(Usuario.UsuarioId, ct) ?? throw new ErroDeAutenticacao("Sessão inválida. Entre novamente.");

        var vinculos = await Grupos.ListarVinculosDoUsuarioAsync(Usuario.UsuarioId, ct);
        foreach (var vinculo in vinculos) vinculo.Desativar();

        var existente = vinculos.FirstOrDefault(v => v.GrupoId == convite.GrupoId);
        if (existente is not null) existente.Ativar();
        else await Grupos.AdicionarMembroAsync(MembroDoGrupo.Criar(convite.GrupoId, Usuario.UsuarioId, PapelNoGrupoEnum.Membro, agora), ct);

        convite.Usar(Usuario.UsuarioId, agora);
        var sessao = await _emissor.EmitirAsync(conta, convite.GrupoId, ct);
        await _unidade.SalvarAsync(ct);
        return sessao;
    }
}

/// <summary>
/// Sai da família e volta ao grupo pessoal. Se quem sai era o único administrador, outro membro
/// (o mais antigo) é promovido, para o grupo nunca ficar sem ninguém que o administre.
/// </summary>
public sealed class SairDoGrupo : CasoDeUsoDeGrupo
{
    private readonly IServicoDeContas _contas;
    private readonly ServicoDeGrupoAtivo _grupoAtivo;
    private readonly EmissorDeSessao _emissor;
    private readonly IUnidadeDeTrabalho _unidade;

    public SairDoGrupo(IGrupoRepository grupos, IUsuarioAtual usuario, IServicoDeContas contas, ServicoDeGrupoAtivo grupoAtivo, EmissorDeSessao emissor, IUnidadeDeTrabalho unidade) : base(grupos, usuario)
    {
        _contas = contas;
        _grupoAtivo = grupoAtivo;
        _emissor = emissor;
        _unidade = unidade;
    }

    public async Task<SessaoResposta> ExecutarAsync(CancellationToken ct)
    {
        var conta = await _contas.ObterPorIdAsync(Usuario.UsuarioId, ct) ?? throw new ErroDeAutenticacao("Sessão inválida. Entre novamente.");
        SessaoResposta? sessao = null;

        await _unidade.ExecutarEmTransacaoAsync(async () =>
        {
            var membros = await Grupos.ListarMembrosAsync(Usuario.GrupoId, ct);
            if (membros.Count <= 1) throw new ErroDeConflito("Você não está em um grupo compartilhado.");

            var meu = membros.First(m => m.UsuarioId == Usuario.UsuarioId);
            var outros = membros.Where(m => m.UsuarioId != Usuario.UsuarioId).ToList();
            if (meu.Administrador && !outros.Any(m => m.Administrador)) outros.OrderBy(m => m.EntrouEm).First().Promover();

            Grupos.RemoverMembro(meu);
            await _unidade.SalvarAsync(ct);

            // Só agora, com a saída já gravada, descobre para onde a pessoa volta.
            var destino = await _grupoAtivo.ResolverAsync(conta, ct);
            sessao = await _emissor.EmitirAsync(conta, destino, ct);
            await _unidade.SalvarAsync(ct);
        }, ct);

        return sessao!;
    }
}

public sealed class RemoverMembroDoGrupo : CasoDeUsoDeGrupo
{
    private readonly IUnidadeDeTrabalho _unidade;

    public RemoverMembroDoGrupo(IGrupoRepository grupos, IUsuarioAtual usuario, IUnidadeDeTrabalho unidade) : base(grupos, usuario)
    {
        _unidade = unidade;
    }

    public async Task ExecutarAsync(Guid usuarioId, CancellationToken ct)
    {
        await ObterVinculoDeAdministradorAsync(ct);
        if (usuarioId == Usuario.UsuarioId) throw new ErroDeValidacao("Para sair do grupo, use a opção \"Sair do grupo\".");

        var alvo = await Grupos.ObterMembroAsync(Usuario.GrupoId, usuarioId, ct) ?? throw new ErroDeNaoEncontrado("Membro não encontrado.");
        Grupos.RemoverMembro(alvo);
        await _unidade.SalvarAsync(ct);
    }
}
