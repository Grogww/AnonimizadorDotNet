using System.Numerics;

namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// Conversões entre cadeias de bytes e inteiros sem sinal em big-endian, conforme NIST SP 800-38G,
/// seção 4.5: NUM(X) e [x]<sup>s</sup>.
/// </summary>
internal static class ByteString
{
    /// <summary>NUM(X): o inteiro sem sinal representado pelos bytes, em big-endian.</summary>
    /// <param name="bytes">Bytes com o mais significativo primeiro.</param>
    /// <returns>O inteiro correspondente; zero para a cadeia vazia.</returns>
    public static BigInteger ToInteger(ReadOnlySpan<byte> bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: true);

    /// <summary>
    /// [x]<sup>s</sup>: escreve <paramref name="value"/> em exatamente <c>destination.Length</c> bytes,
    /// big-endian, com zeros à esquerda.
    /// </summary>
    /// <param name="value">Inteiro no intervalo [0, 256<sup>s</sup>).</param>
    /// <param name="destination">Destino dos s bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">O valor é negativo ou não cabe em s bytes.</exception>
    public static void FromInteger(BigInteger value, Span<byte> destination)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "O valor não pode ser negativo.");
        }

        if (value.IsZero)
        {
            destination.Clear();
            return;
        }

        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > destination.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), "O valor não cabe na quantidade de bytes do destino.");
        }

        int padding = destination.Length - byteCount;
        destination[..padding].Clear();
        value.TryWriteBytes(destination[padding..], out _, isUnsigned: true, isBigEndian: true);
    }
}
