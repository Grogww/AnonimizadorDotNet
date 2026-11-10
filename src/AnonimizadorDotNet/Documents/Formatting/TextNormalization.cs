using System.Globalization;
using System.Text;

namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>Normalização de texto para comparar nomes sem depender de caixa ou de acentos.</summary>
internal static class TextNormalization
{
    /// <summary>
    /// A chave de comparação: sem acentos (inclui "ç" ↦ "c") e em maiúsculas invariantes.
    /// "João", "JOAO" e "joão" têm a mesma chave.
    /// </summary>
    public static string ToKey(ReadOnlySpan<char> text) =>
        RemoveDiacritics(text).ToUpperInvariant();

    /// <summary>Remove os acentos, mantendo as letras-base, e devolve o texto na forma NFC.</summary>
    public static string RemoveDiacritics(ReadOnlySpan<char> text)
    {
        string decomposed = new string(text).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Verdadeiro quando o texto tem alguma letra acentuada.</summary>
    public static bool HasDiacritics(ReadOnlySpan<char> text)
    {
        foreach (char c in text)
        {
            if (c > '\u007F' && char.IsLetter(c) && RemoveDiacritics([c]) != c.ToString())
            {
                return true;
            }
        }

        return false;
    }
}
