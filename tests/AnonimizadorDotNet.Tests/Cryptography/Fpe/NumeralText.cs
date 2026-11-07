namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

/// <summary>
/// Converte entre texto e numerais no alfabeto usado pelos exemplos do NIST (0–9 seguido de a–z).
/// Só para testes: o mapeamento de caracteres da lib pertence à camada de documentos.
/// </summary>
internal static class NumeralText
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    public static byte[] Parse(string text) =>
        text.Select(c => (byte)Alphabet.IndexOf(c, StringComparison.Ordinal)).ToArray();

    public static string Format(ReadOnlySpan<byte> numerals)
    {
        char[] chars = new char[numerals.Length];
        for (int i = 0; i < numerals.Length; i++)
        {
            chars[i] = Alphabet[numerals[i]];
        }

        return new string(chars);
    }
}
