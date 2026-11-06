using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>
/// Implementa a porta de contas usando ASP.NET Identity (hash de senha, bloqueio por tentativas).
/// </summary>
public sealed class ServicoDeContas : IServicoDeContas
{
    // Logins seguidos (ex.: abrir o app em duas abas) não precisam gravar o acesso de novo.
    private static readonly TimeSpan IntervaloMinimoEntreAcessos = TimeSpan.FromMinutes(5);

    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly IRelogio _relogio;

    public ServicoDeContas(UserManager<ApplicationUser> usuarios, IRelogio relogio)
    {
        _usuarios = usuarios;
        _relogio = relogio;
    }

    public async Task<ResultadoDeCriacaoDeConta> CriarAsync(string email, string nome, string senha, CancellationToken ct, bool semSenhaPropria = false)
    {
        var usuario = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = email, Email = email, Nome = nome, CriadoEm = _relogio.AgoraUtc, UltimoAcessoEm = _relogio.AgoraUtc, SemSenhaPropria = semSenhaPropria };
        var resultado = await _usuarios.CreateAsync(usuario, senha);

        if (resultado.Succeeded) return new ResultadoDeCriacaoDeConta(usuario.Id, false, []);

        var duplicado = resultado.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
        return new ResultadoDeCriacaoDeConta(null, duplicado, resultado.Errors.Select(TraduzirErro).ToList());
    }

    public async Task<ContaDoUsuario?> ValidarCredenciaisAsync(string email, string senha, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByEmailAsync(email);
        if (usuario is null) return null;
        if (await _usuarios.IsLockedOutAsync(usuario)) return null;

        if (!await _usuarios.CheckPasswordAsync(usuario, senha))
        {
            await _usuarios.AccessFailedAsync(usuario);
            return null;
        }

        await _usuarios.ResetAccessFailedCountAsync(usuario);
        return ParaConta(usuario);
    }

    public async Task<ContaDoUsuario?> ObterPorIdAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        return usuario is null ? null : ParaConta(usuario);
    }

    public async Task<ContaDoUsuario?> ObterPorEmailAsync(string email, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByEmailAsync(email);
        return usuario is null ? null : ParaConta(usuario);
    }

    private static ContaDoUsuario ParaConta(ApplicationUser usuario) => new(usuario.Id, usuario.Nome, usuario.Email!, usuario.BloqueadoEm is not null, usuario.SemSenhaPropria);

    public async Task<bool> DefinirBloqueioAsync(Guid usuarioId, DateTime? bloqueadoEm, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null) return false;

        usuario.BloqueadoEm = bloqueadoEm;
        await _usuarios.UpdateAsync(usuario);
        return true;
    }

    public async Task<bool> ConferirSenhaAsync(Guid usuarioId, string senha, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        return usuario is not null && await _usuarios.CheckPasswordAsync(usuario, senha);
    }

    public async Task<bool> EstaBloqueadaAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        return usuario is null || usuario.BloqueadoEm is not null;
    }

    public async Task<IReadOnlyList<string>> RedefinirSenhaAsync(Guid usuarioId, string novaSenha, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null) return ["Conta não encontrada."];

        // Valida pelas mesmas regras do cadastro ANTES de gravar: trocar o hash só depois de a senha nova ser aceita.
        var erros = new List<string>();
        foreach (var validador in _usuarios.PasswordValidators)
        {
            var resultado = await validador.ValidateAsync(_usuarios, usuario, novaSenha);
            if (!resultado.Succeeded) erros.AddRange(resultado.Errors.Select(TraduzirErro));
        }

        if (erros.Count > 0) return erros.Distinct().ToList();

        usuario.PasswordHash = _usuarios.PasswordHasher.HashPassword(usuario, novaSenha);
        usuario.SemSenhaPropria = false;
        var gravado = await _usuarios.UpdateAsync(usuario);
        if (!gravado.Succeeded) return ["Não foi possível trocar a senha agora. Tente de novo."];

        // Quem esquece a senha costuma ter errado várias vezes: a conta volta destravada.
        await _usuarios.ResetAccessFailedCountAsync(usuario);
        await _usuarios.SetLockoutEndDateAsync(usuario, null);
        return [];
    }

    public async Task AlterarNomeAsync(Guid usuarioId, string nome, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString()) ?? throw new InvalidOperationException("Conta não encontrada.");
        usuario.Nome = nome;
        await _usuarios.UpdateAsync(usuario);
    }

    public async Task RegistrarAcessoAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null) return;
        if (usuario.UltimoAcessoEm is { } ultimo && agora - ultimo < IntervaloMinimoEntreAcessos) return;

        usuario.UltimoAcessoEm = agora;
        await _usuarios.UpdateAsync(usuario);
    }

    private static string TraduzirErro(IdentityError erro)
    {
        return erro.Code switch
        {
            "PasswordTooShort" => "A senha é curta demais.",
            "PasswordRequiresDigit" => "A senha precisa ter ao menos um número.",
            "PasswordRequiresLower" => "A senha precisa ter ao menos uma letra minúscula.",
            "PasswordRequiresUpper" => "A senha precisa ter ao menos uma letra maiúscula.",
            "InvalidEmail" => "Informe um e-mail válido.",
            "PasswordRequiresNonAlphanumeric" => "A senha precisa ter ao menos um símbolo.",
            _ => "Não foi possível criar a conta com os dados informados.",
        };
    }
}
