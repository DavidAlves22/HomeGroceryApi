namespace HomeGroceryApi.Application.Interfaces;

/// <summary>
/// Abstrai "agora" para os testes controlarem o tempo (expiração de token, datas de criação).
/// </summary>
public interface IRelogio
{
    DateTime AgoraUtc { get; }
}
