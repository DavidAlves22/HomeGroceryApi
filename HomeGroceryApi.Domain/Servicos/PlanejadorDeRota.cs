using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.ObjetosDeValor;

namespace HomeGroceryApi.Domain.Servicos;

/// <summary>Lugar do mapa em que o usuário achou o produto (vale mais que a categoria da seção).</summary>
public sealed record LocalProprio(Guid SecaoId, LadoDaSecaoEnum Lado);

/// <summary>Um item que ainda falta comprar. CategoriaId vazio (categoria apagada) cai em "sem local".</summary>
public sealed record DemandaDeRota(Guid ItemId, Guid? CategoriaId, int OrdemDaCategoria, IReadOnlyList<LocalProprio>? LocaisProprios = null);

public sealed record ParadaDaRota(Guid SecaoId, LadoDaSecaoEnum? Lado, PontoDoMapa Posicao, double Distancia, IReadOnlyList<Guid> ItensIds);

public sealed record PlanoDeRota(IReadOnlyList<ParadaDaRota> Paradas, IReadOnlyList<Guid> ItensSemLocal, IReadOnlyList<PontoDoMapa> Trilha, double Distancia);

/// <summary>
/// Monta a rota dentro de UM mercado. Prateleiras e caixas são obstáculos numa grade; o resto é chão.
/// Os caminhos são os menores possíveis (Dijkstra em 8 direções, sem cortar quina de prateleira) e a ordem
/// das paradas é sempre "o lugar mais perto de onde estou". Sem GPS: o ponto de partida vem de quem chama
/// (a entrada, ou o lugar do último item marcado).
/// </summary>
public static class PlanejadorDeRota
{
    public const double MetrosPorCelula = 1.5;

    private static readonly (int Dx, int Dy)[] Direcoes = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private readonly record struct Lugar(Secao Secao, LadoDaSecaoEnum? Lado);

    /// <param name="otimizar">true = sempre à parada mais próxima; false = segue a ordem das categorias (serve de comparação para mostrar a economia).</param>
    public static PlanoDeRota Planejar(Loja loja, IReadOnlyList<Secao> secoes, IReadOnlyList<SecaoCategoria> vinculos, IReadOnlyList<DemandaDeRota> demandas, PontoDoMapa inicio, bool otimizar)
    {
        var largura = loja.Largura;
        var altura = loja.Altura;
        var bloqueado = new bool[largura * altura];
        foreach (var area in secoes.Select(s => s.Area).Append(loja.Caixas)) Marcar(bloqueado, largura, altura, area);

        var atual = inicio.X < 0 || inicio.Y < 0 || inicio.X >= largura || inicio.Y >= altura || bloqueado[inicio.Y * largura + inicio.X] ? loja.Entrada : inicio;
        var secoesPorId = secoes.ToDictionary(s => s.Id);

        var locaisPorGrupo = vinculos
            .Where(v => secoesPorId.ContainsKey(v.SecaoId))
            .GroupBy(v => v.CategoriaId)
            .ToDictionary(g => g.Key, g => g.Select(v => new Lugar(secoesPorId[v.SecaoId], secoesPorId[v.SecaoId].Dividida ? v.Lado : null)).Distinct().ToList());

        // Cada "grupo" é um conjunto de itens que vão ao mesmo lugar: a categoria (ids de categoria) ou, quando o
        // usuário marcou onde achou o produto, o próprio item (id do item). O local do usuário vale mais que a categoria.
        var semLocal = new List<Guid>();
        var porGrupo = new Dictionary<Guid, (int Ordem, List<Guid> Itens)>();
        foreach (var demanda in demandas)
        {
            var proprios = (demanda.LocaisProprios ?? [])
                .Where(l => secoesPorId.ContainsKey(l.SecaoId))
                .Select(l => new Lugar(secoesPorId[l.SecaoId], secoesPorId[l.SecaoId].Dividida ? l.Lado : null))
                .Distinct()
                .ToList();

            Guid chave;
            if (proprios.Count > 0)
            {
                chave = demanda.ItemId;
                locaisPorGrupo[chave] = proprios;
            }
            else if (demanda.CategoriaId is { } categoria && locaisPorGrupo.ContainsKey(categoria))
            {
                chave = categoria;
            }
            else
            {
                semLocal.Add(demanda.ItemId);
                continue;
            }

            if (!porGrupo.TryGetValue(chave, out var grupo)) porGrupo[chave] = grupo = (demanda.OrdemDaCategoria, []);
            grupo.Itens.Add(demanda.ItemId);
        }

        var categorias = porGrupo.Keys.ToList();
        if (!otimizar) categorias = categorias.OrderBy(c => porGrupo[c].Ordem).ToList();

        var paradas = new List<ParadaDaRota>();
        var pontos = new List<PontoDoMapa> { atual };
        double distancia = 0;

        while (categorias.Count > 0)
        {
            var (dist, anterior) = MenoresCaminhos(bloqueado, largura, altura, atual);
            var alvo = otimizar ? categorias.ToList() : new List<Guid> { categorias[0] };
            (Guid Categoria, Lugar Lugar, PontoDoMapa Ponto, double Distancia)? melhor = null;

            // Cada categoria pode estar em vários lugares: vale o mais perto de quem anda.
            foreach (var categoria in alvo)
            {
                foreach (var lugar in locaisPorGrupo[categoria])
                {
                    var parada = MelhorParada(lugar.Secao.Area, lugar.Lado, bloqueado, largura, altura, dist);
                    if (parada is { } p && (melhor is null || p.Distancia < melhor.Value.Distancia)) melhor = (categoria, lugar, p.Ponto, p.Distancia);
                }
            }

            if (melhor is not { } escolhido)
            {
                foreach (var categoria in alvo) semLocal.AddRange(porGrupo[categoria].Itens);
                categorias.RemoveAll(alvo.Contains);
                continue;
            }

            // Na rota otimizada, numa parada também se pega as outras categorias que moram no mesmo lugar.
            var juntas = otimizar ? categorias.Where(c => locaisPorGrupo[c].Contains(escolhido.Lugar)).ToList() : new List<Guid> { escolhido.Categoria };
            categorias.RemoveAll(juntas.Contains);

            pontos.AddRange(Caminho(anterior, largura, atual, escolhido.Ponto).Skip(1));
            distancia += escolhido.Distancia;
            atual = escolhido.Ponto;
            paradas.Add(new ParadaDaRota(escolhido.Lugar.Secao.Id, escolhido.Lugar.Lado, atual, escolhido.Distancia, juntas.SelectMany(c => porGrupo[c].Itens).ToList()));
        }

        if (paradas.Count > 0)
        {
            var (dist, anterior) = MenoresCaminhos(bloqueado, largura, altura, atual);
            if (MelhorParada(loja.Caixas, null, bloqueado, largura, altura, dist) is { } fim)
            {
                pontos.AddRange(Caminho(anterior, largura, atual, fim.Ponto).Skip(1));
                distancia += fim.Distancia;
            }
        }

        return new PlanoDeRota(paradas, semLocal, Simplificar(pontos), distancia);
    }

    private static void Marcar(bool[] bloqueado, int largura, int altura, AreaDoMapa area)
    {
        for (var y = area.Y; y < area.Y + area.Altura; y++)
        {
            for (var x = area.X; x < area.X + area.Largura; x++)
            {
                if (x >= 0 && x < largura && y >= 0 && y < altura) bloqueado[y * largura + x] = true;
            }
        }
    }

    private static (double[] Distancia, int[] Anterior) MenoresCaminhos(bool[] bloqueado, int largura, int altura, PontoDoMapa origem)
    {
        var distancia = new double[largura * altura];
        var anterior = new int[largura * altura];
        Array.Fill(distancia, double.PositiveInfinity);
        Array.Fill(anterior, -1);

        var fila = new PriorityQueue<int, double>();
        var inicio = origem.Y * largura + origem.X;
        distancia[inicio] = 0;
        fila.Enqueue(inicio, 0);

        while (fila.TryDequeue(out var u, out var d))
        {
            if (d > distancia[u]) continue;
            var ux = u % largura;
            var uy = u / largura;

            foreach (var (dx, dy) in Direcoes)
            {
                var vx = ux + dx;
                var vy = uy + dy;
                if (vx < 0 || vy < 0 || vx >= largura || vy >= altura) continue;

                var v = vy * largura + vx;
                if (bloqueado[v]) continue;

                // Andar na diagonal rente a uma quina de prateleira atravessaria a prateleira.
                if (dx != 0 && dy != 0 && (bloqueado[uy * largura + vx] || bloqueado[vy * largura + ux])) continue;

                var novo = d + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                if (novo >= distancia[v]) continue;

                distancia[v] = novo;
                anterior[v] = u;
                fila.Enqueue(v, novo);
            }
        }

        return (distancia, anterior);
    }

    /// <summary>
    /// Células coladas na seção. Com "lado" (gôndola de dois lados) só vale o corredor daquele lado e as
    /// pontas da metade correspondente: o outro lado fica de fora, mesmo que esteja mais perto.
    /// </summary>
    private static List<PontoDoMapa> Vizinhas(AreaDoMapa r, LadoDaSecaoEnum? lado)
    {
        var cima = Enumerable.Range(r.X, r.Largura).Select(x => new PontoDoMapa(x, r.Y - 1)).ToList();
        var baixo = Enumerable.Range(r.X, r.Largura).Select(x => new PontoDoMapa(x, r.Y + r.Altura)).ToList();
        var esquerda = Enumerable.Range(r.Y, r.Altura).Select(y => new PontoDoMapa(r.X - 1, y)).ToList();
        var direita = Enumerable.Range(r.Y, r.Altura).Select(y => new PontoDoMapa(r.X + r.Largura, y)).ToList();

        if (lado is null) return [.. cima, .. baixo, .. esquerda, .. direita];

        var ehA = lado == LadoDaSecaoEnum.A;
        if (r.Largura >= r.Altura)
        {
            var meio = r.Y + r.Altura / 2.0;
            bool Metade(PontoDoMapa p) => ehA ? p.Y < meio : p.Y >= meio;
            return [.. (ehA ? cima : baixo), .. esquerda.Where(Metade), .. direita.Where(Metade)];
        }
        else
        {
            var meio = r.X + r.Largura / 2.0;
            bool Metade(PontoDoMapa p) => ehA ? p.X < meio : p.X >= meio;
            return [.. (ehA ? esquerda : direita), .. cima.Where(Metade), .. baixo.Where(Metade)];
        }
    }

    private static (PontoDoMapa Ponto, double Distancia)? MelhorParada(AreaDoMapa area, LadoDaSecaoEnum? lado, bool[] bloqueado, int largura, int altura, double[] distancia)
    {
        (PontoDoMapa, double)? melhor = null;
        foreach (var p in Vizinhas(area, lado))
        {
            if (p.X < 0 || p.Y < 0 || p.X >= largura || p.Y >= altura) continue;

            var indice = p.Y * largura + p.X;
            if (bloqueado[indice] || double.IsInfinity(distancia[indice])) continue;
            if (melhor is null || distancia[indice] < melhor.Value.Item2) melhor = (p, distancia[indice]);
        }

        return melhor;
    }

    private static List<PontoDoMapa> Caminho(int[] anterior, int largura, PontoDoMapa de, PontoDoMapa para)
    {
        var origem = de.Y * largura + de.X;
        var caminho = new List<PontoDoMapa>();
        for (var c = para.Y * largura + para.X; c != -1; c = anterior[c])
        {
            caminho.Add(new PontoDoMapa(c % largura, c / largura));
            if (c == origem) break;
        }

        caminho.Reverse();
        return caminho;
    }

    /// <summary>Tira pontos repetidos e os do meio de trechos retos, para a trilha ter só as curvas.</summary>
    private static List<PontoDoMapa> Simplificar(List<PontoDoMapa> pontos)
    {
        var unicos = pontos.Where((p, i) => i == 0 || p != pontos[i - 1]).ToList();
        return unicos.Where((p, i) =>
        {
            if (i == 0 || i == unicos.Count - 1) return true;
            var a = unicos[i - 1];
            var c = unicos[i + 1];
            var cruz = (p.X - a.X) * (c.Y - p.Y) - (p.Y - a.Y) * (c.X - p.X);
            var ponto = (p.X - a.X) * (c.X - p.X) + (p.Y - a.Y) * (c.Y - p.Y);
            return cruz != 0 || ponto < 0;
        }).ToList();
    }
}
