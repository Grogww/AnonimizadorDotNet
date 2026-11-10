using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Cpf;

/// <summary>Um CPF válido já normalizado: a base de 9 numerais e a representação original.</summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class ParsedCpf
{
    private readonly byte[] _base;

    internal ParsedCpf(byte[] baseNumerals, TextLayout layout)
    {
        _base = baseNumerals;
        Layout = layout;
    }

    /// <summary>Os 9 primeiros dígitos (sem os verificadores), como numerais 0–9: a parte cifrada.</summary>
    public ReadOnlySpan<byte> Base => _base;

    /// <summary>A representação original (máscara, texto ou número, zeros à esquerda).</summary>
    public TextLayout Layout { get; }
}
