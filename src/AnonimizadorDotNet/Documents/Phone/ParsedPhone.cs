using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Phone;

/// <summary>
/// Um telefone brasileiro já normalizado: o que é preservado (código do país, prefixo de longa distância,
/// DDD, o 9 do celular e o primeiro dígito do assinante) e os 7 dígitos finais, que são cifrados.
/// </summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class ParsedPhone
{
    private readonly char[] _preserved;
    private readonly byte[] _subscriber;

    internal ParsedPhone(char[] preserved, byte[] subscriber, string? ddd, bool isMobile, TextLayout layout)
    {
        _preserved = preserved;
        _subscriber = subscriber;
        Ddd = ddd;
        IsMobile = isMobile;
        Layout = layout;
    }

    /// <summary>Os dígitos preservados, na ordem em que aparecem.</summary>
    public ReadOnlySpan<char> Preserved => _preserved;

    /// <summary>Os 7 dígitos finais como numerais 0–9: a parte cifrada.</summary>
    public ReadOnlySpan<byte> Subscriber => _subscriber;

    /// <summary>O DDD, ou nulo quando o número veio sem ele.</summary>
    public string? Ddd { get; }

    /// <summary>Verdadeiro para celular (9 dígitos começando por 9).</summary>
    public bool IsMobile { get; }

    /// <summary>A representação original (máscara, texto ou número).</summary>
    public TextLayout Layout { get; }
}
