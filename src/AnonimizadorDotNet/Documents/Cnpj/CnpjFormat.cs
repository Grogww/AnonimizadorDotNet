using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Cryptography.Fpe;
using AnonimizadorDotNet.Documents.CheckDigits;
using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Cnpj;

/// <summary>Normalização, validação e formatação de CNPJ, numérico e alfanumérico.</summary>
/// <remarks>
/// <para>
/// Um CNPJ tem 14 posições: raiz (8), ordem (4) e dois dígitos verificadores. No formato alfanumérico
/// da Receita Federal, as 12 primeiras posições aceitam letras e os verificadores continuam numéricos; o
/// valor de cada posição no cálculo é o código ASCII − 48, o que no CNPJ numérico coincide com o dígito.
/// </para>
/// <para>
/// A raiz decide o domínio da cifra: só dígitos ⇒ base 10, proibidos os dígitos todos iguais
/// (<see cref="IsAllowedNumericRoot"/>); com alguma letra ⇒ base 36, exigida ao menos uma letra
/// (<see cref="IsAllowedAlphanumericRoot"/>), para que o substituto não caia no domínio numérico. A ordem é
/// preservada e não pode ser <c>0000</c>.
/// </para>
/// <para>
/// Aceita máscara (<c>12.ABC.345/01DE-35</c>), sem máscara e, para o CNPJ só de dígitos, sem os zeros à
/// esquerda. Letras minúsculas são aceitas; se todas as letras da entrada estiverem em minúsculas, a saída
/// também fica; caso contrário, em maiúsculas, como no padrão da Receita.
/// </para>
/// </remarks>
internal static class CnpjFormat
{
    /// <summary>Posições da raiz.</summary>
    public const int RootLength = 8;

    /// <summary>Posições da ordem.</summary>
    public const int OrderLength = 4;

    /// <summary>Posições totais, com os verificadores.</summary>
    public const int Length = 14;

    private const int BodyLength = RootLength + OrderLength;

    private static readonly SearchValues<char> Alphanumerics =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    private static readonly SearchValues<char> Separators = SearchValues.Create("./- ");

    /// <summary>Raiz numérica permitida: não pode ter todos os dígitos iguais.</summary>
    public static readonly NumeralPredicate IsAllowedNumericRoot = numerals => !Numerals.AllEqual(numerals);

    /// <summary>Raiz alfanumérica permitida: ao menos uma letra (numeral de 10 a 35).</summary>
    public static readonly NumeralPredicate IsAllowedAlphanumericRoot =
        numerals => numerals.IndexOfAnyInRange((byte)10, (byte)35) >= 0;

    /// <summary>Normaliza e valida um CNPJ.</summary>
    /// <param name="input">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="cnpj">O CNPJ normalizado.</param>
    /// <returns>Falso se o formato, o tamanho, a ordem ou os verificadores estiverem errados, ou se a raiz
    /// for proibida. Nunca lança exceção por causa do conteúdo.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, ValueKind kind, [NotNullWhen(true)] out ParsedCnpj? cnpj)
    {
        cnpj = null;
        Span<char> chars = stackalloc char[Length];
        if (!TextLayout.TryCapture(input, Alphanumerics, Separators, kind, chars, out int written, out TextLayout? layout))
        {
            return false;
        }

        // Um número só tem dígitos: o CNPJ alfanumérico sempre chega como texto.
        if (kind == ValueKind.Number && chars[..written].ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        LetterCase letterCase = LetterCasing.Detect(chars[..written]);
        if (written < Length)
        {
            // Sem os zeros à esquerda: só vale sem máscara e para o CNPJ só de dígitos.
            if (layout.HasSeparators || chars[..written].ContainsAnyExceptInRange('0', '9'))
            {
                return false;
            }

            if (!layout.LeadingZerosOmitted)
            {
                layout = layout.OmittingLeadingZeros();
            }

            int missing = Length - written;
            chars[..written].CopyTo(chars[missing..]);
            chars[..missing].Fill('0');
        }

        for (int i = 0; i < Length; i++)
        {
            chars[i] = char.ToUpperInvariant(chars[i]);
        }

        ReadOnlySpan<char> body = chars[..BodyLength];
        ReadOnlySpan<char> checkChars = chars[BodyLength..];
        if (checkChars.ContainsAnyExceptInRange('0', '9') || body[RootLength..].SequenceEqual("0000"))
        {
            return false;
        }

        Span<byte> checkDigits = stackalloc byte[2];
        ComputeCheckDigits(body, checkDigits);
        if (checkChars[0] - '0' != checkDigits[0] || checkChars[1] - '0' != checkDigits[1])
        {
            return false;
        }

        ReadOnlySpan<char> rootChars = body[..RootLength];
        bool alphanumeric = rootChars.ContainsAnyExceptInRange('0', '9');
        byte[] root = new byte[RootLength];
        Numerals.TryFromChars(rootChars, alphanumeric ? 36 : 10, root);
        bool allowed = alphanumeric ? IsAllowedAlphanumericRoot(root) : IsAllowedNumericRoot(root);
        if (!allowed)
        {
            return false;
        }

        cnpj = new ParsedCnpj(root, body[RootLength..].ToArray(), alphanumeric, letterCase, layout);
        return true;
    }

    /// <summary>
    /// Monta o CNPJ com uma nova raiz e a ordem do original, recalculando os verificadores e reaplicando a
    /// representação e a caixa.
    /// </summary>
    /// <param name="root">8 numerais no radix da raiz original (<see cref="ParsedCnpj.RootRadix"/>).</param>
    /// <param name="original">O CNPJ cuja ordem, representação e caixa são reaplicadas.</param>
    /// <exception cref="ArgumentException">A raiz não tem 8 numerais no radix esperado.</exception>
    public static string Format(ReadOnlySpan<byte> root, ParsedCnpj original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (root.Length != RootLength)
        {
            throw new ArgumentException($"A raiz do CNPJ tem {RootLength} posições.", nameof(root));
        }

        Span<char> chars = stackalloc char[Length];
        Numerals.ToChars(root, original.RootRadix, chars);
        original.Order.CopyTo(chars[RootLength..]);

        Span<byte> checkDigits = stackalloc byte[2];
        ComputeCheckDigits(chars[..BodyLength], checkDigits);
        chars[BodyLength] = (char)('0' + checkDigits[0]);
        chars[BodyLength + 1] = (char)('0' + checkDigits[1]);

        if (original.LetterCase == LetterCase.Lower)
        {
            for (int i = 0; i < Length; i++)
            {
                chars[i] = char.ToLowerInvariant(chars[i]);
            }
        }

        return original.Layout.Apply(chars);
    }

    /// <summary>
    /// Calcula os verificadores das 12 primeiras posições (dígitos ou letras maiúsculas), com valor
    /// ASCII − 48 por posição.
    /// </summary>
    /// <exception cref="ArgumentException">Tamanho errado ou caractere fora de 0–9 e A–Z.</exception>
    public static void ComputeCheckDigits(ReadOnlySpan<char> body, Span<byte> checkDigits)
    {
        if (body.Length != BodyLength || checkDigits.Length < 2)
        {
            throw new ArgumentException($"O corpo do CNPJ tem {BodyLength} posições e o destino, 2.", nameof(body));
        }

        Span<int> values = stackalloc int[BodyLength + 1];
        for (int i = 0; i < BodyLength; i++)
        {
            char c = body[i];
            if (!char.IsAsciiDigit(c) && !char.IsAsciiLetterUpper(c))
            {
                throw new ArgumentException("O corpo do CNPJ só aceita dígitos e letras maiúsculas.", nameof(body));
            }

            values[i] = c - '0';
        }

        // Pesos de 2 a 9, da direita para a esquerda, recomeçando depois do 9.
        ReadOnlySpan<int> weights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        checkDigits[0] = Modulo11.Compute(values[..BodyLength], weights[1..]);
        values[BodyLength] = checkDigits[0];
        checkDigits[1] = Modulo11.Compute(values, weights);
    }
}
