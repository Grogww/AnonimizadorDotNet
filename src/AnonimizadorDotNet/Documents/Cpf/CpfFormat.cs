using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Cryptography.Fpe;
using AnonimizadorDotNet.Documents.CheckDigits;
using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Cpf;

/// <summary>Normalização, validação e formatação de CPF.</summary>
/// <remarks>
/// <para>
/// Aceita o CPF com máscara (<c>529.982.247-25</c>), sem máscara (<c>52998224725</c>) e sem os zeros à
/// esquerda, como fica quando é guardado num inteiro (<c>1234567890</c> é <c>012.345.678-90</c>). A saída
/// segue a mesma representação.
/// </para>
/// <para>
/// Os CPFs com todos os dígitos iguais têm verificadores corretos, mas nenhum validador os aceita: são
/// recusados na entrada e proibidos na saída (<see cref="IsAllowedBase"/>, usado no <i>cycle-walking</i>).
/// </para>
/// </remarks>
internal static class CpfFormat
{
    /// <summary>Quantidade de dígitos da base (a parte cifrada).</summary>
    public const int BaseLength = 9;

    /// <summary>Quantidade total de dígitos, com os verificadores.</summary>
    public const int Length = 11;

    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> Separators = SearchValues.Create(".- ");

    /// <summary>Base permitida: não pode ter todos os dígitos iguais.</summary>
    public static readonly NumeralPredicate IsAllowedBase = numerals => !Numerals.AllEqual(numerals);

    /// <summary>Normaliza e valida um CPF.</summary>
    /// <param name="input">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="cpf">O CPF normalizado.</param>
    /// <returns>Falso se o formato, o tamanho ou os verificadores estiverem errados, ou se a base for proibida.
    /// Nunca lança exceção por causa do conteúdo.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, ValueKind kind, [NotNullWhen(true)] out ParsedCpf? cpf)
    {
        cpf = null;
        Span<char> chars = stackalloc char[Length];
        if (!TextLayout.TryCapture(input, Digits, Separators, kind, chars, out int written, out TextLayout? layout))
        {
            return false;
        }

        if (written < Length)
        {
            // Sem os zeros à esquerda: só vale sem máscara (número ou texto sem preenchimento).
            if (layout.HasSeparators)
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

        Span<byte> numerals = stackalloc byte[Length];
        Numerals.TryFromChars(chars, 10, numerals);

        Span<byte> checkDigits = stackalloc byte[2];
        ComputeCheckDigits(numerals[..BaseLength], checkDigits);
        if (!numerals[BaseLength..].SequenceEqual(checkDigits) || !IsAllowedBase(numerals[..BaseLength]))
        {
            return false;
        }

        cpf = new ParsedCpf(numerals[..BaseLength].ToArray(), layout);
        return true;
    }

    /// <summary>Monta o CPF a partir da base, recalculando os verificadores e reaplicando a representação.</summary>
    /// <param name="baseNumerals">9 numerais de 0 a 9.</param>
    /// <param name="layout">A representação a reaplicar (a de um <see cref="ParsedCpf"/>).</param>
    /// <exception cref="ArgumentException">A base não tem 9 numerais de 0 a 9.</exception>
    public static string Format(ReadOnlySpan<byte> baseNumerals, TextLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (baseNumerals.Length != BaseLength)
        {
            throw new ArgumentException($"A base do CPF tem {BaseLength} dígitos.", nameof(baseNumerals));
        }

        Span<byte> numerals = stackalloc byte[Length];
        baseNumerals.CopyTo(numerals);
        ComputeCheckDigits(baseNumerals, numerals[BaseLength..]);

        Span<char> chars = stackalloc char[Length];
        Numerals.ToChars(numerals, 10, chars);
        return layout.Apply(chars);
    }

    /// <summary>Calcula os dois dígitos verificadores de uma base de 9 numerais.</summary>
    public static void ComputeCheckDigits(ReadOnlySpan<byte> baseNumerals, Span<byte> checkDigits)
    {
        if (baseNumerals.Length != BaseLength || checkDigits.Length < 2)
        {
            throw new ArgumentException($"A base do CPF tem {BaseLength} dígitos e o destino, 2.", nameof(baseNumerals));
        }

        Span<int> values = stackalloc int[BaseLength + 1];
        for (int i = 0; i < BaseLength; i++)
        {
            values[i] = baseNumerals[i];
        }

        // Pesos de 10 a 2 para o primeiro dígito e de 11 a 2 para o segundo.
        ReadOnlySpan<int> weights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        checkDigits[0] = Modulo11.Compute(values[..BaseLength], weights[1..]);
        values[BaseLength] = checkDigits[0];
        checkDigits[1] = Modulo11.Compute(values, weights);
    }
}
