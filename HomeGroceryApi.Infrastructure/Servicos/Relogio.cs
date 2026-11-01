using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Infrastructure.Servicos;

public sealed class Relogio : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
