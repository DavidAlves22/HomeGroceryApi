namespace HomeGroceryApi.Application.Opcoes;

/// <summary>
/// Quem é administrador do app (painel em /admin). A lista vem da configuração (Admin__Emails__0, Admin__Emails__1...),
/// nunca do cadastro: nenhum campo enviado pelo cliente consegue dar esse papel a alguém.
/// </summary>
public sealed class AdminOpcoes
{
    public const string Secao = "Admin";

    public string[] Emails { get; set; } = [];
}
