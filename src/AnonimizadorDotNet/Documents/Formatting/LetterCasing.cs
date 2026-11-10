using System.Globalization;

namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>Detecta e reaplica o padrão de caixa das letras (<see cref="LetterCase"/>).</summary>
/// <remarks>
/// Uma palavra começa no início do texto ou depois de espaço, hífen ou apóstrofo. As conversões usam a
/// cultura invariante, para que o resultado não dependa da máquina.
/// </remarks>
internal static class LetterCasing
{
    /// <summary>Detecta o padrão de caixa das letras do texto.</summary>
    /// <returns>
    /// <see cref="LetterCase.Upper"/> ou <see cref="LetterCase.Lower"/> quando todas as letras seguem a mesma
    /// caixa (texto em maiúsculas tem prioridade sobre título, mesmo com uma letra por palavra);
    /// <see cref="LetterCase.Title"/> quando cada palavra começa em maiúscula e segue em minúsculas;
    /// <see cref="LetterCase.Unknown"/> sem letras ou com caixa mista.
    /// </returns>
    public static LetterCase Detect(ReadOnlySpan<char> text)
    {
        bool hasLetter = false;
        bool allUpper = true;
        bool allLower = true;
        bool title = true;
        bool wordStart = true;

        foreach (char c in text)
        {
            if (IsWordBoundary(c))
            {
                wordStart = true;
                continue;
            }

            if (!char.IsLetter(c))
            {
                continue;
            }

            hasLetter = true;
            bool upper = char.IsUpper(c);
            bool lower = char.IsLower(c);
            allUpper &= upper;
            allLower &= lower;
            title &= wordStart ? upper : lower;
            wordStart = false;
        }

        if (!hasLetter)
        {
            return LetterCase.Unknown;
        }

        if (allUpper)
        {
            return LetterCase.Upper;
        }

        if (allLower)
        {
            return LetterCase.Lower;
        }

        return title ? LetterCase.Title : LetterCase.Unknown;
    }

    /// <summary>Aplica o padrão de caixa ao texto; <see cref="LetterCase.Unknown"/> o deixa como está.</summary>
    public static string Apply(ReadOnlySpan<char> text, LetterCase letterCase)
    {
        Span<char> output = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
        bool wordStart = true;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            output[i] = letterCase switch
            {
                LetterCase.Upper => char.ToUpperInvariant(c),
                LetterCase.Lower => char.ToLowerInvariant(c),
                LetterCase.Title => wordStart ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c),
                _ => c,
            };

            if (IsWordBoundary(c))
            {
                wordStart = true;
            }
            else if (char.IsLetter(c))
            {
                wordStart = false;
            }
        }

        return new string(output);
    }

    private static bool IsWordBoundary(char c) =>
        char.IsWhiteSpace(c) || c is '-' or '\'' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DashPunctuation;
}
