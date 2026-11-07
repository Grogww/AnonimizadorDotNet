using System.Numerics;

namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// Conversões entre cadeias de numerais e inteiros, conforme NIST SP 800-38G, seção 4.5.
/// </summary>
/// <remarks>
/// Uma cadeia de numerais é uma sequência de valores entre 0 e radix − 1, um por byte, com o numeral
/// mais significativo primeiro. O mapeamento entre numerais e caracteres (como "0"–"9" ou "0"–"z")
/// não pertence a esta camada.
/// </remarks>
internal static class NumeralString
{
    /// <summary>
    /// NUM<sub>radix</sub>(X): o inteiro representado pela cadeia de numerais na base informada.
    /// </summary>
    /// <param name="numerals">Numerais, cada um menor que <paramref name="radix"/>.</param>
    /// <param name="radix">Base da representação.</param>
    /// <returns>O inteiro correspondente; zero para a cadeia vazia.</returns>
    public static BigInteger ToInteger(ReadOnlySpan<byte> numerals, int radix)
    {
        BigInteger value = BigInteger.Zero;
        foreach (byte numeral in numerals)
        {
            value = (value * radix) + numeral;
        }

        return value;
    }

    /// <summary>
    /// STR<sup>m</sup><sub>radix</sub>(x): a representação de <paramref name="value"/> com exatamente
    /// <c>destination.Length</c> numerais, com zeros à esquerda.
    /// </summary>
    /// <param name="value">Inteiro no intervalo [0, radix<sup>m</sup>).</param>
    /// <param name="radix">Base da representação.</param>
    /// <param name="destination">Destino dos m numerais.</param>
    /// <exception cref="ArgumentOutOfRangeException">O valor é negativo ou não cabe em m numerais.</exception>
    public static void FromInteger(BigInteger value, int radix, Span<byte> destination)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "O valor não pode ser negativo.");
        }

        BigInteger remaining = value;
        for (int i = destination.Length - 1; i >= 0; i--)
        {
            remaining = BigInteger.DivRem(remaining, radix, out BigInteger numeral);
            destination[i] = (byte)numeral;
        }

        if (!remaining.IsZero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), "O valor não cabe na quantidade de numerais do destino.");
        }
    }
}
