using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Excecoes;
using HomeGroceryApi.Domain.ObjetosDeValor;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Tests.Dominio;

public class RegrasDoDominioTests
{
    private static readonly DateTime Agora = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-11", "Novembro de 2026")]
    [InlineData("2030-01", "Janeiro de 2030")]
    public void Competencia_valida_expoe_o_nome_do_mes(string chave, string nome)
    {
        Assert.Equal(nome, Competencia.Parse(chave).Nome);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("26-11")]
    [InlineData("")]
    [InlineData(null)]
    public void Competencia_invalida_e_recusada(string? chave)
    {
        Assert.Throws<ErroDeDominio>(() => Competencia.Parse(chave));
    }

    [Fact]
    public void Segunda_lista_principal_no_mes_exige_confirmacao_explicita()
    {
        var competencia = Competencia.Parse("2026-11");

        Assert.Throws<ErroDeDominio>(() => RegrasDeLista.DefinirPrincipalENome(competencia, null, true, false));

        var (principal, nome) = RegrasDeLista.DefinirPrincipalENome(competencia, "Festa", true, true);
        Assert.False(principal);
        Assert.Equal("Festa", nome);
    }

    [Fact]
    public void Primeira_lista_do_mes_e_principal_e_leva_o_nome_do_mes()
    {
        var (principal, nome) = RegrasDeLista.DefinirPrincipalENome(Competencia.Parse("2026-11"), "  ", false, false);

        Assert.True(principal);
        Assert.Equal("Novembro de 2026", nome);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(28, 12, 43)]
    [InlineData(3, 1, 33)]
    [InlineData(3, 2, 67)]
    [InlineData(10, 10, 100)]
    public void Percentual_de_conclusao_e_arredondado(int total, int comprados, int esperado)
    {
        Assert.Equal(esperado, RegrasDeLista.PercentualConcluido(total, comprados));
    }

    [Fact]
    public void Lista_finalizada_nao_aceita_edicao_ate_ser_reaberta()
    {
        var lista = ListaDeCompras.Criar(Guid.NewGuid(), Guid.NewGuid(), "2026-11", "Novembro", true, Guid.NewGuid(), Agora);

        lista.Finalizar(Agora);

        Assert.True(lista.Finalizada);
        Assert.Equal(Agora, lista.FinalizadaEm);
        Assert.Throws<ErroDeDominio>(() => lista.GarantirEditavel());
        Assert.Throws<ErroDeDominio>(() => lista.Finalizar(Agora));

        lista.Reabrir();
        lista.GarantirEditavel();
        Assert.Null(lista.FinalizadaEm);
    }

    [Fact]
    public void Marcar_comprado_e_idempotente_e_so_sobe_a_versao_quando_muda()
    {
        var usuario = Guid.NewGuid();
        var item = ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Arroz", null, "Mercearia", UnidadeDeMedidaEnum.Pacote, 2, 0, usuario);
        Assert.Equal(1, item.Versao);

        item.DefinirComprado(true, usuario, Agora);
        item.DefinirComprado(true, usuario, Agora.AddMinutes(5));

        Assert.True(item.Comprado);
        Assert.Equal(Agora, item.CompradoEm);
        Assert.Equal(usuario, item.CompradoPorUsuarioId);
        Assert.Equal(2, item.Versao);

        item.DefinirComprado(false, usuario, Agora);
        Assert.False(item.Comprado);
        Assert.Null(item.CompradoPorUsuarioId);
        Assert.Equal(3, item.Versao);
    }

    [Fact]
    public void Editar_item_guarda_o_novo_snapshot_e_sobe_a_versao()
    {
        var item = ItemDaLista.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Arroz", null, "Mercearia", UnidadeDeMedidaEnum.Pacote, 2, 0, Guid.NewGuid());
        var categoria = Guid.NewGuid();

        item.Editar(Guid.NewGuid(), "Arroz integral", categoria, "Grãos", UnidadeDeMedidaEnum.Kg, 5);

        Assert.Equal("Arroz integral", item.NomeSnapshot);
        Assert.Equal("Grãos", item.CategoriaNomeSnapshot);
        Assert.Equal(5, item.QuantidadePlanejada);
        Assert.Equal(2, item.Versao);
    }
}
