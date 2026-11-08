using System.Numerics;

namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// Limites dos parâmetros do FF1 (NIST SP 800-38G Rev. 1, seção 5.2) e as restrições adicionais desta
/// implementação.
/// </summary>
internal static class Ff1Constraints
{
    /// <summary>Menor radix aceito.</summary>
    public const int MinRadix = 2;

    /// <summary>
    /// Maior radix aceito. A norma permite até 2<sup>16</sup>; esta implementação limita a 36, o que
    /// cobre os alfabetos 0–9 e 0–9 + A–Z e permite um numeral por byte.
    /// </summary>
    public const int MaxRadix = 36;

    /// <summary>Tamanho mínimo do domínio exigido pela norma: radix<sup>minlen</sup> ≥ 1.000.000.</summary>
    public const int MinDomainSize = 1_000_000;

    /// <summary>Maior quantidade de numerais por entrada aceita por esta implementação.</summary>
    public const int MaxLength = 256;

    /// <summary>Maior tamanho de tweak, em bytes, aceito por esta implementação.</summary>
    public const int MaxTweakLength = 256;

    /// <summary>
    /// Menor quantidade de numerais permitida para o radix: o menor n ≥ 2 com radix<sup>n</sup> ≥ 10⁶.
    /// </summary>
    /// <param name="radix">Radix já validado.</param>
    public static int GetMinLength(int radix)
    {
        ValidateRadix(radix, nameof(radix));

        int length = 2;
        BigInteger domainSize = (BigInteger)radix * radix;
        while (domainSize < MinDomainSize)
        {
            domainSize *= radix;
            length++;
        }

        return length;
    }

    /// <summary>Garante que o radix está entre <see cref="MinRadix"/> e <see cref="MaxRadix"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Radix fora do intervalo.</exception>
    public static void ValidateRadix(int radix, string paramName)
    {
        if (radix is < MinRadix or > MaxRadix)
        {
            throw new ArgumentOutOfRangeException(paramName, "O radix deve estar entre 2 e 36.");
        }
    }

    /// <summary>Garante que a quantidade de numerais está entre o mínimo do radix e <see cref="MaxLength"/>.</summary>
    /// <exception cref="ArgumentException">Quantidade de numerais fora do intervalo.</exception>
    public static void ValidateLength(int length, int minLength, string paramName)
    {
        if (length < minLength)
        {
            throw new ArgumentException(
                $"A entrada deve ter ao menos {minLength} numerais para este radix (domínio mínimo de 10⁶ valores).",
                paramName);
        }

        if (length > MaxLength)
        {
            throw new ArgumentException($"A entrada deve ter no máximo {MaxLength} numerais.", paramName);
        }
    }

    /// <summary>Garante que o tweak não passa de <see cref="MaxTweakLength"/> bytes.</summary>
    /// <exception cref="ArgumentException">Tweak longo demais.</exception>
    public static void ValidateTweak(int tweakLength, string paramName)
    {
        if (tweakLength > MaxTweakLength)
        {
            throw new ArgumentException($"O tweak deve ter no máximo {MaxTweakLength} bytes.", paramName);
        }
    }
}
