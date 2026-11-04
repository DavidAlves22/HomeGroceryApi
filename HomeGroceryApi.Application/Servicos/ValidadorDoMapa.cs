using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Application.Servicos;

/// <summary>
/// Confere o mapa enviado pelo editor. O app já impede sobreposição ao arrastar, mas a API é a última barreira:
/// um mapa com seções empilhadas ou fora da grade quebraria o cálculo da rota.
/// </summary>
public static class ValidadorDoMapa
{
    public const int TamanhoMinimo = 10;
    public const int TamanhoMaximo = 120;
    public const int MaximoDeSecoes = 150;

    public static void Validar(SalvarMapaRequisicao mapa, IReadOnlySet<Guid> categoriasValidas)
    {
        if (mapa.Largura is < TamanhoMinimo or > TamanhoMaximo || mapa.Altura is < TamanhoMinimo or > TamanhoMaximo) throw new ErroDeValidacao($"O mapa deve ter de {TamanhoMinimo} a {TamanhoMaximo} células de largura e de altura.");
        if (mapa.Secoes.Count > MaximoDeSecoes) throw new ErroDeValidacao($"O mapa aceita no máximo {MaximoDeSecoes} seções.");

        var caixas = mapa.Caixas.ParaArea();
        var entrada = new PontoDoMapa(mapa.Entrada.X, mapa.Entrada.Y);
        if (!caixas.CabeEm(mapa.Largura, mapa.Altura)) throw new ErroDeValidacao("Os caixas precisam estar dentro do mapa.");
        if (!new AreaDoMapa(entrada.X, entrada.Y, 1, 1).CabeEm(mapa.Largura, mapa.Altura)) throw new ErroDeValidacao("A entrada precisa estar dentro do mapa.");
        if (caixas.Contem(entrada)) throw new ErroDeValidacao("A entrada não pode ficar em cima dos caixas.");

        var ocupadas = new List<AreaDoMapa> { caixas };
        foreach (var secao in mapa.Secoes)
        {
            var area = secao.Area.ParaArea();
            var nome = secao.Nome?.Trim() ?? string.Empty;
            if (nome.Length is 0 or > 40) throw new ErroDeValidacao("O nome da seção é obrigatório e pode ter no máximo 40 caracteres.");
            if (!area.CabeEm(mapa.Largura, mapa.Altura)) throw new ErroDeValidacao($"A seção \"{nome}\" precisa estar dentro do mapa.");
            if (area.Contem(entrada)) throw new ErroDeValidacao($"A seção \"{nome}\" não pode ficar em cima da entrada.");
            if (ocupadas.Any(o => o.Intersecta(area))) throw new ErroDeValidacao($"A seção \"{nome}\" está sobreposta a outra seção ou aos caixas.");
            ocupadas.Add(area);

            if (secao.Dividida)
            {
                var nomeB = secao.NomeLadoB?.Trim() ?? string.Empty;
                if (nomeB.Length is 0 or > 40) throw new ErroDeValidacao($"Informe o nome do outro lado da gôndola \"{nome}\" (até 40 caracteres).");

                // Cada lado precisa de pelo menos uma célula de espessura.
                if (Math.Min(area.Largura, area.Altura) < 2) throw new ErroDeValidacao($"A gôndola de dois lados \"{nome}\" precisa ter pelo menos 2 células de espessura.");
            }
            else if (secao.CategoriasLadoB is { Count: > 0 })
            {
                throw new ErroDeValidacao($"A seção \"{nome}\" tem um lado só: use apenas o lado A.");
            }

            ValidarCategorias(nome, secao.CategoriasLadoA, categoriasValidas);
            ValidarCategorias(nome, secao.CategoriasLadoB, categoriasValidas);
        }
    }

    private static void ValidarCategorias(string secao, IReadOnlyList<Guid>? categorias, IReadOnlySet<Guid> validas)
    {
        if (categorias is null) return;
        if (categorias.Distinct().Count() != categorias.Count) throw new ErroDeValidacao($"A seção \"{secao}\" repete uma categoria no mesmo lado.");
        if (categorias.Any(c => !validas.Contains(c))) throw new ErroDeValidacao($"A seção \"{secao}\" usa uma categoria que não existe.");
    }
}
