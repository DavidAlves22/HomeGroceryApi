using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Servicos;
using HomeGroceryApi.Application.UseCases.Autenticacao;
using HomeGroceryApi.Tests.Fakes;

namespace HomeGroceryApi.Tests.UseCases;

public class AutenticacaoTests
{
    private readonly ServicoDeContasFake _contas = new();
    private readonly GrupoRepositoryFake _grupos = new();
    private readonly TokenDeRenovacaoRepositoryFake _tokensDeRenovacao = new();
    private readonly UnidadeDeTrabalhoFake _unidade = new();
    private readonly TokenServiceFake _tokens = new();
    private readonly RelogioFixo _relogio = new();

    private EmissorDeSessao Emissor() => new(_tokens, _tokensDeRenovacao);

    private CadastrarUsuario Cadastro() => new(_contas, _grupos, _unidade, Emissor(), _relogio);

    private ServicoDeGrupoAtivo GrupoAtivo() => new(_grupos, _relogio);

    private FazerLogin Login() => new(_contas, GrupoAtivo(), _unidade, Emissor());

    private RenovarSessao Renovacao() => new(_tokens, _tokensDeRenovacao, _contas, GrupoAtivo(), _unidade, Emissor(), _relogio);

    private static CadastroRequisicao CadastroValido(string email = "maria@exemplo.com") => new("Maria", email, "senha1234");

    [Fact]
    public async Task Cadastro_cria_grupo_pessoal_com_as_11_categorias_iniciais()
    {
        var sessao = await Cadastro().ExecutarAsync(CadastroValido(), default);

        Assert.Single(_grupos.Grupos);
        Assert.True(_grupos.Grupos[0].Pessoal);
        Assert.Equal(sessao.GrupoId, _grupos.Grupos[0].Id);
        Assert.Equal(11, _grupos.Categorias.Count);
        Assert.Equal("Mercearia", _grupos.Categorias.OrderBy(c => c.Ordem).First().Nome);
        Assert.All(_grupos.Categorias, c => Assert.Equal(sessao.GrupoId, c.GrupoId));
    }

    [Fact]
    public async Task Cadastro_normaliza_o_email_e_devolve_sessao()
    {
        var sessao = await Cadastro().ExecutarAsync(CadastroValido("  Maria@Exemplo.COM "), default);

        Assert.Equal("maria@exemplo.com", sessao.Usuario.Email);
        Assert.False(string.IsNullOrEmpty(sessao.TokenDeAcesso));
        Assert.False(string.IsNullOrEmpty(sessao.TokenDeRenovacao));
    }

    [Fact]
    public async Task Cadastro_com_email_repetido_gera_conflito()
    {
        await Cadastro().ExecutarAsync(CadastroValido(), default);

        await Assert.ThrowsAsync<ErroDeConflito>(() => Cadastro().ExecutarAsync(CadastroValido(), default));
    }

    [Theory]
    [InlineData("", "maria@exemplo.com", "senha1234")]
    [InlineData("Maria", "email-invalido", "senha1234")]
    [InlineData("Maria", "maria@exemplo.com", "curta")]
    public async Task Cadastro_com_dados_invalidos_gera_erro_de_validacao(string nome, string email, string senha)
    {
        await Assert.ThrowsAsync<ErroDeValidacao>(() => Cadastro().ExecutarAsync(new CadastroRequisicao(nome, email, senha), default));
        Assert.Empty(_grupos.Grupos);
    }

    [Fact]
    public async Task Login_com_senha_correta_devolve_o_grupo_do_usuario()
    {
        var cadastro = await Cadastro().ExecutarAsync(CadastroValido(), default);

        var sessao = await Login().ExecutarAsync(new LoginRequisicao("maria@exemplo.com", "senha1234"), default);

        Assert.Equal(cadastro.GrupoId, sessao.GrupoId);
    }

    [Theory]
    [InlineData("maria@exemplo.com", "senha-errada")]
    [InlineData("outra@exemplo.com", "senha1234")]
    public async Task Login_invalido_usa_a_mesma_mensagem_para_email_e_senha(string email, string senha)
    {
        await Cadastro().ExecutarAsync(CadastroValido(), default);

        var erro = await Assert.ThrowsAsync<ErroDeAutenticacao>(() => Login().ExecutarAsync(new LoginRequisicao(email, senha), default));

        Assert.Equal("E-mail ou senha incorretos.", erro.Message);
    }

    [Fact]
    public async Task Renovar_revoga_o_token_antigo_e_ele_nao_serve_de_novo()
    {
        var sessao = await Cadastro().ExecutarAsync(CadastroValido(), default);
        var requisicao = new RenovarSessaoRequisicao(sessao.TokenDeRenovacao);

        var nova = await Renovacao().ExecutarAsync(requisicao, default);

        Assert.NotEqual(sessao.TokenDeRenovacao, nova.TokenDeRenovacao);
        await Assert.ThrowsAsync<ErroDeAutenticacao>(() => Renovacao().ExecutarAsync(requisicao, default));
    }

    [Fact]
    public async Task Renovar_com_token_expirado_e_recusado()
    {
        var sessao = await Cadastro().ExecutarAsync(CadastroValido(), default);
        _relogio.AgoraUtc = _relogio.AgoraUtc.AddDays(60);

        await Assert.ThrowsAsync<ErroDeAutenticacao>(() => Renovacao().ExecutarAsync(new RenovarSessaoRequisicao(sessao.TokenDeRenovacao), default));
    }
}
