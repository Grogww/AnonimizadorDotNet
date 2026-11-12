namespace AnonimizadorDotNet.Detection;

/// <summary>A posição de um trecho encontrado por um <see cref="ITextDetector"/>.</summary>
/// <remarks>Guarda só a posição, nunca o conteúdo do trecho.</remarks>
public readonly record struct TextMatch
{
    /// <summary>Cria a posição de um trecho.</summary>
    /// <param name="index">Onde o trecho começa (a partir de 0).</param>
    /// <param name="length">Quantidade de caracteres do trecho, maior que zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Posição negativa ou tamanho menor que 1.</exception>
    public TextMatch(int index, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        Index = index;
        Length = length;
    }

    /// <summary>Onde o trecho começa (a partir de 0).</summary>
    public int Index { get; }

    /// <summary>Quantidade de caracteres do trecho.</summary>
    public int Length { get; }
}
