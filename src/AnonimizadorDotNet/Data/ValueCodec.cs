using System.Globalization;

namespace AnonimizadorDotNet.Data;

/// <summary>Converte um valor de coluna para o texto que os transformadores recebem, e o substituto de volta.</summary>
/// <remarks>
/// <para>
/// Texto (<see cref="string"/>, <c>char[]</c>, <see cref="char"/>) vai como <see cref="ValueKind.Text"/>.
/// Inteiros e <see cref="decimal"/> sem casas decimais vão como <see cref="ValueKind.Number"/>, só com os
/// dígitos (formato invariante). Qualquer outro valor não é convertido.
/// </para>
/// <para>
/// A volta usa o tipo do valor original: um <see cref="long"/> volta como <see cref="long"/>. Se o substituto
/// não couber no tipo (um CPF substituto numa coluna <see cref="int"/>), a conversão falha. Nenhum dos dois
/// métodos lança exceção por causa do conteúdo.
/// </para>
/// </remarks>
internal static class ValueCodec
{
    /// <summary>O texto a transformar e como ele chegou.</summary>
    /// <returns>Falso se o valor não for texto nem inteiro (inclusive <see cref="decimal"/> com casas).</returns>
    public static bool TryEncode(object value, out string text, out ValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(value);
        kind = ValueKind.Number;
        switch (value)
        {
            case string s:
                text = s;
                kind = ValueKind.Text;
                return true;
            case char[] chars:
                text = new string(chars);
                kind = ValueKind.Text;
                return true;
            case char c:
                text = c.ToString();
                kind = ValueKind.Text;
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                text = ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
                return true;
            case decimal d when decimal.Truncate(d) == d:
                text = d.ToString("F0", CultureInfo.InvariantCulture);
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    /// <summary>O substituto convertido para o tipo do valor original.</summary>
    /// <returns>Falso se o substituto não couber no tipo do original.</returns>
    public static bool TryDecode(string substitute, object original, out object result)
    {
        ArgumentNullException.ThrowIfNull(substitute);
        ArgumentNullException.ThrowIfNull(original);
        const NumberStyles Digits = NumberStyles.None;
        CultureInfo invariant = CultureInfo.InvariantCulture;

        object? decoded = original switch
        {
            string => substitute,
            char[] => substitute.ToCharArray(),
            char => substitute.Length == 1 ? substitute[0] : null,
            sbyte => sbyte.TryParse(substitute, Digits, invariant, out sbyte v) ? v : null,
            byte => byte.TryParse(substitute, Digits, invariant, out byte v) ? v : null,
            short => short.TryParse(substitute, Digits, invariant, out short v) ? v : null,
            ushort => ushort.TryParse(substitute, Digits, invariant, out ushort v) ? v : null,
            int => int.TryParse(substitute, Digits, invariant, out int v) ? v : null,
            uint => uint.TryParse(substitute, Digits, invariant, out uint v) ? v : null,
            long => long.TryParse(substitute, Digits, invariant, out long v) ? v : null,
            ulong => ulong.TryParse(substitute, Digits, invariant, out ulong v) ? v : null,
            decimal => decimal.TryParse(substitute, Digits, invariant, out decimal v) ? v : null,
            _ => null,
        };

        result = decoded ?? original;
        return decoded is not null;
    }
}
