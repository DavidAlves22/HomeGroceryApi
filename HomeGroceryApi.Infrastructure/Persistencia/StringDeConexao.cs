using Npgsql;

namespace HomeGroceryApi.Infrastructure.Persistencia;

/// <summary>
/// Aceita a conexão com o PostgreSQL nos dois formatos que existem na prática: o do Npgsql
/// ("Host=...;Database=...") e o em URL que serviços como Neon e Render entregam
/// ("postgresql://usuario:senha@host/banco?sslmode=require"). Assim dá para colar o que o serviço mostra.
/// </summary>
public static class StringDeConexao
{
    public static string Normalizar(string conexao)
    {
        var texto = conexao.Trim();
        if (!texto.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !texto.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return texto;

        var uri = new Uri(texto);
        var credenciais = uri.UserInfo.Split(':', 2);
        var consulta = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var construtor = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credenciais[0]),
            Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null,
        };

        // Serviços na nuvem exigem conexão criptografada; sem sslmode informado, assume o seguro.
        construtor.SslMode = (consulta["sslmode"] ?? "require").ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "prefer" => SslMode.Prefer,
            "verify-full" => SslMode.VerifyFull,
            "verify-ca" => SslMode.VerifyCA,
            _ => SslMode.Require,
        };

        return construtor.ConnectionString;
    }
}
