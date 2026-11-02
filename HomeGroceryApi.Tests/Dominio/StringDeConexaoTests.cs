using HomeGroceryApi.Infrastructure.Persistencia;
using Npgsql;

namespace HomeGroceryApi.Tests.Dominio;

public class StringDeConexaoTests
{
    [Fact]
    public void Converte_a_url_do_Neon_para_o_formato_do_Npgsql()
    {
        var resultado = StringDeConexao.Normalizar("postgresql://dono:s3nh%40forte@ep-exemplo-123.us-east-2.aws.neon.tech/homegrocery?sslmode=require&channel_binding=require");

        var c = new NpgsqlConnectionStringBuilder(resultado);
        Assert.Equal("ep-exemplo-123.us-east-2.aws.neon.tech", c.Host);
        Assert.Equal(5432, c.Port);
        Assert.Equal("homegrocery", c.Database);
        Assert.Equal("dono", c.Username);
        Assert.Equal("s3nh@forte", c.Password);
        Assert.Equal(SslMode.Require, c.SslMode);
    }

    [Fact]
    public void Aceita_o_esquema_postgres_porta_explicita_e_assume_ssl_quando_nao_informado()
    {
        var c = new NpgsqlConnectionStringBuilder(StringDeConexao.Normalizar("postgres://u:p@host.exemplo:6543/banco"));

        Assert.Equal(6543, c.Port);
        Assert.Equal(SslMode.Require, c.SslMode);
    }

    [Fact]
    public void Respeita_sslmode_disable_e_deixa_o_formato_do_Npgsql_intacto()
    {
        var url = new NpgsqlConnectionStringBuilder(StringDeConexao.Normalizar("postgresql://u:p@localhost/b?sslmode=disable"));
        Assert.Equal(SslMode.Disable, url.SslMode);

        const string classica = "Host=localhost;Port=5433;Database=homegrocery;Username=u;Password=p";
        Assert.Equal(classica, StringDeConexao.Normalizar(classica));
    }
}
