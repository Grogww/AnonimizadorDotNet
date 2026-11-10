using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Cnpj;

/// <summary>
/// Um CNPJ válido já normalizado: a raiz de 8 posições (a parte cifrada), a ordem preservada e a
/// representação original.
/// </summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class ParsedCnpj
{
    private readonly byte[] _root;
    private readonly char[] _order;

    internal ParsedCnpj(byte[] root, char[] order, bool isAlphanumericRoot, LetterCase letterCase, TextLayout layout)
    {
        _root = root;
        _order = order;
        IsAlphanumericRoot = isAlphanumericRoot;
        LetterCase = letterCase;
        Layout = layout;
    }

    /// <summary>
    /// A raiz como numerais: 0–9 quando numérica, 0–35 (base 36) quando alfanumérica.
    /// </summary>
    public ReadOnlySpan<byte> Root => _root;

    /// <summary>A ordem (estabelecimento), 4 caracteres em maiúsculas; não é cifrada.</summary>
    public ReadOnlySpan<char> Order => _order;

    /// <summary>
    /// Verdadeiro quando a raiz tem ao menos uma letra: cifra em base 36 com <i>cycle-walking</i> até haver
    /// letra. Falso: raiz numérica, cifrada em base 10.
    /// </summary>
    public bool IsAlphanumericRoot { get; }

    /// <summary>Radix da raiz: 36 ou 10.</summary>
    public int RootRadix => IsAlphanumericRoot ? 36 : 10;

    /// <summary>Caixa das letras na entrada; só <see cref="LetterCase.Lower"/> muda a saída.</summary>
    public LetterCase LetterCase { get; }

    /// <summary>A representação original (máscara, texto ou número, zeros à esquerda).</summary>
    public TextLayout Layout { get; }
}
