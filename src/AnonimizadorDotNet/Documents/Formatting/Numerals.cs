namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>
/// Mapeamento entre caracteres e numerais (valores de 0 a radix − 1, um por byte) usados pelo FF1.
/// </summary>
/// <remarks>
/// Base 10: "0"–"9" ↦ 0–9. Base 36: "0"–"9" ↦ 0–9 e "A"–"Z" ↦ 10–35, sem distinguir caixa na entrada;
/// a saída usa maiúsculas e a caixa original é reaplicada pela camada do documento.
/// </remarks>
internal static class Numerals
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Converte caracteres em numerais na base informada (10 ou 36).</summary>
    /// <returns>Falso se algum caractere não pertencer ao alfabeto da base.</returns>
    public static bool TryFromChars(ReadOnlySpan<char> chars, int radix, Span<byte> numerals)
    {
        ValidateRadix(radix);
        if (numerals.Length < chars.Length)
        {
            throw new ArgumentException("O destino é menor que a entrada.", nameof(numerals));
        }

        for (int i = 0; i < chars.Length; i++)
        {
            int value = ValueOf(chars[i]);
            if (value < 0 || value >= radix)
            {
                return false;
            }

            numerals[i] = (byte)value;
        }

        return true;
    }

    /// <summary>Converte numerais em caracteres ("0"–"9", "A"–"Z").</summary>
    /// <exception cref="ArgumentException">Algum numeral está fora da base.</exception>
    public static void ToChars(ReadOnlySpan<byte> numerals, int radix, Span<char> chars)
    {
        ValidateRadix(radix);
        if (chars.Length < numerals.Length)
        {
            throw new ArgumentException("O destino é menor que a entrada.", nameof(chars));
        }

        for (int i = 0; i < numerals.Length; i++)
        {
            if (numerals[i] >= radix)
            {
                throw new ArgumentException("Numeral fora da base.", nameof(numerals));
            }

            chars[i] = Alphabet[numerals[i]];
        }
    }

    /// <summary>Verdadeiro quando todos os numerais são iguais (inclui a cadeia vazia).</summary>
    public static bool AllEqual(ReadOnlySpan<byte> numerals) =>
        numerals.IsEmpty || numerals.IndexOfAnyExcept(numerals[0]) < 0;

    private static int ValueOf(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => c - 'A' + 10,
        >= 'a' and <= 'z' => c - 'a' + 10,
        _ => -1,
    };

    private static void ValidateRadix(int radix)
    {
        if (radix is not (10 or 36))
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Base suportada: 10 ou 36.");
        }
    }
}
