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
    private readonly UserManager<ApplicationUser> _usuarios;

    public ServicoDeContas(UserManager<ApplicationUser> usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<ResultadoDeCriacaoDeConta> CriarAsync(string email, string nome, string senha, CancellationToken ct)
    {
        var usuario = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = email, Email = email, Nome = nome };
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
        return new ContaDoUsuario(usuario.Id, usuario.Nome, usuario.Email!);
    }

    public async Task<ContaDoUsuario?> ObterPorIdAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        return usuario is null ? null : new ContaDoUsuario(usuario.Id, usuario.Nome, usuario.Email!);
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
            _ => "Não foi possível criar a conta com os dados informados.",
        };
    }
}
