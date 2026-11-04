using System.Security.Cryptography;
using System.Text;
using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Interfaces;

namespace HomeGroceryApi.Application.UseCases.Autenticacao;

internal static class CodigosDeRecuperacao
{
    public const string MensagemInvalido = "Código inválido ou expirado. Peça um novo código.";

    /// <summary>O hash leva o usuário junto: o mesmo código de dois usuários nunca gera o mesmo hash.</summary>
    public static string Hash(ITokenService tokens, Guid usuarioId, string codigo) => tokens.CalcularHash($"{usuarioId:N}:{codigo}");

    public static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>O app só mostra "Esqueci minha senha" quando o servidor consegue enviar e-mail.</summary>
public sealed class ConsultarRecuperacaoDeSenha
{
    private readonly IEnviadorDeEmail _email;

    public ConsultarRecuperacaoDeSenha(IEnviadorDeEmail email)
    {
        _email = email;
    }

    public RecuperacaoResposta Executar() => new(_email.Habilitado);
}

/// <summary>
/// Envia um código de 6 dígitos para o e-mail da conta. **Responde igual exista ou não a conta** (ninguém descobre quais
/// e-mails têm cadastro) e se limita a um código por 45 segundos e cinco por hora, para não virar disparador de e-mail.
/// </summary>
public sealed class SolicitarRecuperacaoDeSenha
{
    private const int ValidadeEmMinutos = 15;
    private const int MaximoPorHora = 5;
    private const int IntervaloEmSegundos = 45;

    private readonly IServicoDeContas _contas;
    private readonly ICodigoDeRecuperacaoRepository _codigos;
    private readonly IEnviadorDeEmail _email;
    private readonly ITokenService _tokens;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public SolicitarRecuperacaoDeSenha(IServicoDeContas contas, ICodigoDeRecuperacaoRepository codigos, IEnviadorDeEmail email, ITokenService tokens, IUnidadeDeTrabalho unidade, IRelogio relogio)
    {
        _contas = contas;
        _codigos = codigos;
        _email = email;
        _tokens = tokens;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(EsqueciSenhaRequisicao requisicao, CancellationToken ct)
    {
        if (!_email.Habilitado) return;

        var conta = await _contas.ObterPorEmailAsync(CodigosDeRecuperacao.Normalizar(requisicao.Email), ct);
        if (conta is null) return;

        var agora = _relogio.AgoraUtc;
        if (await _codigos.ContarDesdeAsync(conta.Id, agora.AddHours(-1), ct) >= MaximoPorHora) return;
        if (await _codigos.ContarDesdeAsync(conta.Id, agora.AddSeconds(-IntervaloEmSegundos), ct) > 0) return;

        await _codigos.InvalidarAtivosAsync(conta.Id, agora, ct);
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await _codigos.AdicionarAsync(CodigoDeRecuperacao.Criar(Guid.CreateVersion7(), conta.Id, CodigosDeRecuperacao.Hash(_tokens, conta.Id, codigo), agora, agora.AddMinutes(ValidadeEmMinutos)), ct);
        await _unidade.SalvarAsync(ct);

        var texto = $"Olá, {conta.Nome.Split(' ')[0]}!\n\nSeu código para redefinir a senha do Home Grocery é:\n\n    {codigo}\n\nEle vale por {ValidadeEmMinutos} minutos e só pode ser usado uma vez.\nSe não foi você quem pediu, ignore este e-mail: sua senha continua a mesma.";
        await _email.EnviarAsync(conta.Email, "Seu código do Home Grocery", texto, ct);
    }
}

/// <summary>
/// Troca a senha com o código recebido por e-mail. Qualquer falha de código (conta que não existe, vencido, errado,
/// excesso de tentativas) dá a MESMA resposta. Senha fraca não gasta o código. Ao trocar, todas as sessões abertas
/// são encerradas: quem estava com a senha antiga precisa entrar de novo.
/// </summary>
public sealed class RedefinirSenha
{
    private readonly IServicoDeContas _contas;
    private readonly ICodigoDeRecuperacaoRepository _codigos;
    private readonly ITokenDeRenovacaoRepository _sessoes;
    private readonly ITokenService _tokens;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public RedefinirSenha(IServicoDeContas contas, ICodigoDeRecuperacaoRepository codigos, ITokenDeRenovacaoRepository sessoes, ITokenService tokens, IUnidadeDeTrabalho unidade, IRelogio relogio)
    {
        _contas = contas;
        _codigos = codigos;
        _sessoes = sessoes;
        _tokens = tokens;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(RedefinirSenhaRequisicao requisicao, CancellationToken ct)
    {
        var agora = _relogio.AgoraUtc;
        var codigo = new string((requisicao.Codigo ?? string.Empty).Where(char.IsDigit).ToArray());
        var conta = await _contas.ObterPorEmailAsync(CodigosDeRecuperacao.Normalizar(requisicao.Email), ct) ?? throw new ErroDeValidacao(CodigosDeRecuperacao.MensagemInvalido);
        var registro = await _codigos.ObterAtivoMaisRecenteAsync(conta.Id, agora, ct) ?? throw new ErroDeValidacao(CodigosDeRecuperacao.MensagemInvalido);

        var esperado = Encoding.UTF8.GetBytes(registro.CodigoHash);
        var informado = Encoding.UTF8.GetBytes(CodigosDeRecuperacao.Hash(_tokens, conta.Id, codigo));
        if (codigo.Length != 6 || !CryptographicOperations.FixedTimeEquals(esperado, informado))
        {
            registro.RegistrarTentativaErrada();
            await _unidade.SalvarAsync(ct);
            throw new ErroDeValidacao(CodigosDeRecuperacao.MensagemInvalido);
        }

        var erros = await _contas.RedefinirSenhaAsync(conta.Id, requisicao.NovaSenha ?? string.Empty, ct);
        if (erros.Count > 0) throw new ErroDeValidacao(erros);

        registro.MarcarComoUsado(agora);
        await _unidade.SalvarAsync(ct);
        await _sessoes.RevogarTodosDoUsuarioAsync(conta.Id, agora, ct);
    }
}
