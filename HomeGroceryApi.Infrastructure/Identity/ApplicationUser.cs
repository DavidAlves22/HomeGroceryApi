using Microsoft.AspNetCore.Identity;

namespace HomeGroceryApi.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
}
