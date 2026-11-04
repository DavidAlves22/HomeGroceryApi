using System.Net.Mail;
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

/// <summary>
/// Cria a conta e, na mesma transação, o grupo pessoal do usuário com as categorias
/// iniciais. Exemplo: "Maria" se cadastra e já ganha o grupo "Casa de Maria" com Mercearia, Açougue...
/// </summary>
public sealed class CadastrarUsuario
{
    private const int TamanhoMinimoDaSenha = 8;

    private readonly IServicoDeContas _contas;
    private readonly IGrupoRepository _grupos;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly EmissorDeSessao _emissor;
    private readonly IRelogio _relogio;

    public CadastrarUsuario(IServicoDeContas contas, IGrupoRepository grupos, IUnidadeDeTrabalho unidadeDeTrabalho, EmissorDeSessao emissor, IRelogio relogio)
    {
        _contas = contas;
        _grupos = grupos;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _emissor = emissor;
        _relogio = relogio;
    }

    public async Task<SessaoResposta> ExecutarAsync(CadastroRequisicao requisicao, CancellationToken ct)
    {
        var nome = (requisicao.Nome ?? string.Empty).Trim();
        var email = (requisicao.Email ?? string.Empty).Trim().ToLowerInvariant();
        Validar(nome, email, requisicao.Senha);

        SessaoResposta? sessao = null;

        await _unidadeDeTrabalho.ExecutarEmTransacaoAsync(async () =>
        {
            var criacao = await _contas.CriarAsync(email, nome, requisicao.Senha, ct);
            if (criacao.EmailJaCadastrado) throw new ErroDeConflito("Já existe uma conta com este e-mail.");
            if (!criacao.Sucesso) throw new ErroDeValidacao(criacao.Erros);

            var usuarioId = criacao.UsuarioId!.Value;
            var agora = _relogio.AgoraUtc;
            var grupo = Grupo.CriarPessoal(Guid.CreateVersion7(), nome, agora);
            var membro = MembroDoGrupo.Criar(grupo.Id, usuarioId, PapelNoGrupoEnum.Administrador, agora);
            var categorias = CategoriasIniciais.CriarPara(grupo.Id, Guid.CreateVersion7);

            await _grupos.AdicionarAsync(grupo, membro, categorias, ct);
            sessao = await _emissor.EmitirAsync(new ContaDoUsuario(usuarioId, nome, email), grupo.Id, ct);
            await _unidadeDeTrabalho.SalvarAsync(ct);
        }, ct);

        return sessao!;
    }

    private static void Validar(string nome, string email, string? senha)
    {
        var erros = new List<string>();

        if (nome.Length is < 2 or > 80) erros.Add("Informe um nome entre 2 e 80 caracteres.");
        if (!EmailValido(email)) erros.Add("Informe um e-mail válido.");
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimoDaSenha) erros.Add($"A senha precisa ter ao menos {TamanhoMinimoDaSenha} caracteres.");

        if (erros.Count > 0) throw new ErroDeValidacao(erros);
    }

    private static bool EmailValido(string email)
    {
        return MailAddress.TryCreate(email, out var endereco) && endereco.Address == email;
    }
}
