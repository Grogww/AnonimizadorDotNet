using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Phone;

/// <summary>Normalização, validação e formatação de telefones brasileiros (fixo e celular).</summary>
/// <remarks>
/// <para>
/// Formatos aceitos, pela quantidade de dígitos: assinante sozinho (8 ou 9), com DDD (10 ou 11), com o
/// prefixo de longa distância <c>0</c> (11 ou 12) ou com o código do país <c>55</c> (12 ou 13), com ou sem
/// <c>+</c>, parênteses, hífen, pontos e espaços.
/// </para>
/// <para>
/// O DDD precisa ser um dos 67 em uso. O assinante de 9 dígitos é celular e começa por 9; o de 8 dígitos
/// começa de 2 a 9 (fixos de 2 a 5 e celulares antigos, sem o 9, de 6 a 9).
/// </para>
/// <para>
/// Só os 7 dígitos finais são cifrados. O primeiro dígito do assinante (e o 9 do celular) é preservado:
/// é ele que distingue fixo de celular, então o substituto fica sempre na mesma faixa sem precisar de
/// <i>cycle-walking</i>, mesmo para números antigos fora das faixas atuais.
/// </para>
/// </remarks>
internal static class PhoneFormat
{
    /// <summary>Quantidade de dígitos cifrados.</summary>
    public const int SubscriberLength = 7;

    private const int MaxDigits = 13;

    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> Separators = SearchValues.Create("+()-. ");

    /// <summary>Os DDDs em uso no Brasil.</summary>
    public static readonly FrozenSet<string> ValidDdds = new[]
    {
        "11", "12", "13", "14", "15", "16", "17", "18", "19",
        "21", "22", "24", "27", "28",
        "31", "32", "33", "34", "35", "37", "38",
        "41", "42", "43", "44", "45", "46", "47", "48", "49",
        "51", "53", "54", "55",
        "61", "62", "63", "64", "65", "66", "67", "68", "69",
        "71", "73", "74", "75", "77", "79",
        "81", "82", "83", "84", "85", "86", "87", "88", "89",
        "91", "92", "93", "94", "95", "96", "97", "98", "99",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Normaliza e valida um telefone.</summary>
    /// <param name="input">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="phone">O telefone normalizado.</param>
    /// <returns>Falso se o formato, o DDD ou a faixa do assinante não forem válidos. Nunca lança exceção por
    /// causa do conteúdo.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, ValueKind kind, [NotNullWhen(true)] out ParsedPhone? phone)
    {
        phone = null;
        Span<char> digits = stackalloc char[MaxDigits];
        if (!TextLayout.TryCapture(input, Digits, Separators, kind, digits, out int count, out TextLayout? layout))
        {
            return false;
        }

        digits = digits[..count];
        if (!TrySplit(digits, out int prefixLength, out int subscriberLength, out bool hasCountryCode))
        {
            return false;
        }

        if (!IsPlusSignValid(input, hasCountryCode))
        {
            return false;
        }

        string? ddd = prefixLength == 0 ? null : new string(digits.Slice(prefixLength - 2, 2));
        if (ddd is not null && !ValidDdds.Contains(ddd))
        {
            return false;
        }

        ReadOnlySpan<char> subscriber = digits[prefixLength..];
        bool isMobile = subscriberLength == 9;
        bool validRange = isMobile ? subscriber[0] == '9' : subscriber[0] is >= '2' and <= '9';
        if (!validRange)
        {
            return false;
        }

        byte[] encrypted = new byte[SubscriberLength];
        Numerals.TryFromChars(digits[^SubscriberLength..], 10, encrypted);
        phone = new ParsedPhone(digits[..^SubscriberLength].ToArray(), encrypted, ddd, isMobile, layout);
        return true;
    }

    /// <summary>Monta o telefone com novos 7 dígitos finais, reaplicando a representação original.</summary>
    /// <param name="subscriber">7 numerais de 0 a 9.</param>
    /// <param name="original">O telefone cuja parte preservada e representação são reaplicadas.</param>
    /// <exception cref="ArgumentException">Não são 7 numerais de 0 a 9.</exception>
    public static string Format(ReadOnlySpan<byte> subscriber, ParsedPhone original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (subscriber.Length != SubscriberLength)
        {
            throw new ArgumentException($"O telefone tem {SubscriberLength} dígitos cifrados.", nameof(subscriber));
        }

        ReadOnlySpan<char> preserved = original.Preserved;
        Span<char> digits = stackalloc char[preserved.Length + SubscriberLength];
        preserved.CopyTo(digits);
        Numerals.ToChars(subscriber, 10, digits[preserved.Length..]);
        return original.Layout.Apply(digits);
    }

    /// <summary>Separa o prefixo (país, longa distância, DDD) do assinante pela quantidade de dígitos.</summary>
    private static bool TrySplit(ReadOnlySpan<char> digits, out int prefixLength, out int subscriberLength, out bool hasCountryCode)
    {
        hasCountryCode = false;
        bool country = digits.StartsWith("55");
        bool trunk = digits.Length > 0 && digits[0] == '0';
        (prefixLength, subscriberLength) = digits.Length switch
        {
            8 => (0, 8),
            9 => (0, 9),
            10 => (2, 8),
            11 when trunk => (3, 8),
            11 => (2, 9),
            12 when trunk => (3, 9),
            12 when country => (4, 8),
            13 when country => (4, 9),
            _ => (-1, -1),
        };

        if (prefixLength < 0)
        {
            return false;
        }

        hasCountryCode = prefixLength == 4;
        return true;
    }

    /// <summary>O <c>+</c> só pode aparecer uma vez, antes de todos os dígitos e com o código do país.</summary>
    private static bool IsPlusSignValid(ReadOnlySpan<char> input, bool hasCountryCode)
    {
        int plus = input.IndexOf('+');
        if (plus < 0)
        {
            return true;
        }

        return hasCountryCode
            && input[(plus + 1)..].IndexOf('+') < 0
            && input[..plus].IndexOfAny(Digits) < 0;
    }
}
