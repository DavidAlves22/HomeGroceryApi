using Microsoft.AspNetCore.Identity;

namespace HomeGroceryApi.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>Quando a conta nasceu (alimenta o painel de administração).</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Último login ou renovação de sessão: o app renova a cada ~30 min de uso, então serve de "visto por último".</summary>
    public DateTime? UltimoAcessoEm { get; set; }
}
