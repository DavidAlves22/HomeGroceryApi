namespace HomeGroceryApi.Domain.ObjetosDeValor;

/// <summary>Uma célula da grade do mercado. Cada célula vale cerca de 1,5 m.</summary>
public readonly record struct PontoDoMapa(int X, int Y);

/// <summary>Retângulo na grade do mercado, em células (X e Y são o canto de cima à esquerda).</summary>
public readonly record struct AreaDoMapa(int X, int Y, int Largura, int Altura)
{
    public bool Intersecta(AreaDoMapa outra) => X < outra.X + outra.Largura && outra.X < X + Largura && Y < outra.Y + outra.Altura && outra.Y < Y + Altura;

    public bool Contem(PontoDoMapa ponto) => ponto.X >= X && ponto.X < X + Largura && ponto.Y >= Y && ponto.Y < Y + Altura;

    public bool CabeEm(int largura, int altura) => Largura >= 1 && Altura >= 1 && X >= 0 && Y >= 0 && X + Largura <= largura && Y + Altura <= altura;
}
