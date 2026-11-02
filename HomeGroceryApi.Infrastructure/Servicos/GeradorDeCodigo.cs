using System.Security.Cryptography;
using HomeGroceryApi.Application.Interfaces;

namespace HomeGroceryApi.Infrastructure.Servicos;

/// <summary>
/// Código de convite de 8 caracteres com gerador criptográfico (não dá para prever o próximo).
/// O alfabeto não tem O/0 nem I/1/L, para ninguém errar ao ler o código em voz alta.
/// </summary>
public sealed class GeradorDeCodigo : IGeradorDeCodigo
{
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Gerar()
    {
        return string.Create(8, 0, (destino, _) =>
        {
            for (var i = 0; i < destino.Length; i++) destino[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        });
    }
}
