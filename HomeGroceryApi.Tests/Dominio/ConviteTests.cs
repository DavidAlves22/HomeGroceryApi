using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Excecoes;

namespace HomeGroceryApi.Tests.Dominio;

public class ConviteTests
{
    private static readonly DateTime Agora = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Convite Novo() => Convite.Criar(Guid.NewGuid(), Guid.NewGuid(), "K7M2XQ9P", Guid.NewGuid(), Agora);

    [Fact]
    public void Convite_novo_vale_por_7_dias()
    {
        var convite = Novo();

        Assert.True(convite.EstaValido(Agora));
        Assert.True(convite.EstaValido(Agora.AddDays(6).AddHours(23)));
        Assert.False(convite.EstaValido(Agora.AddDays(7)));
    }

    [Fact]
    public void Convite_so_pode_ser_usado_uma_vez()
    {
        var convite = Novo();
        var usuario = Guid.NewGuid();

        convite.Usar(usuario, Agora.AddMinutes(1));

        Assert.False(convite.EstaValido(Agora.AddMinutes(2)));
        Assert.Equal(usuario, convite.UsadoPorUsuarioId);
        Assert.Throws<ErroDeDominio>(() => convite.Usar(Guid.NewGuid(), Agora.AddMinutes(3)));
    }

    [Fact]
    public void Convite_expirado_nao_pode_ser_usado()
    {
        Assert.Throws<ErroDeDominio>(() => Novo().Usar(Guid.NewGuid(), Agora.AddDays(8)));
    }

    [Theory]
    [InlineData("k7m2-xq9p", "K7M2XQ9P")]
    [InlineData(" K7M2 XQ9P ", "K7M2XQ9P")]
    [InlineData("K7M2XQ9P", "K7M2XQ9P")]
    [InlineData(null, "")]
    public void Codigo_e_normalizado_para_comparar(string? digitado, string esperado)
    {
        Assert.Equal(esperado, Convite.NormalizarCodigo(digitado));
    }
}
