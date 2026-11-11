using System.Text;
using AnonimizadorDotNet.Cryptography.Keys;
using Org.BouncyCastle.Crypto.Fpe;
using Org.BouncyCastle.Crypto.Parameters;

namespace AnonimizadorDotNet.Tests.Transformation;

/// <summary>
/// Substitutos calculados sem o código de transformação da lib: FF1 do BouncyCastle, <i>cycle-walking</i> e
/// dígitos verificadores escritos à mão aqui. Só a derivação de subchaves vem da lib, e ela já é fixada por
/// valores dourados calculados fora do .NET (<c>SubkeyDerivationTests</c>).
/// </summary>
internal static class ReferenceSubstitutes
{
    /// <summary>Chave mestra fixa dos testes: os bytes 0x00 a 0x1F.</summary>
    public static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>CPF substituto, só dígitos, a partir de 11 dígitos.</summary>
    public static string Cpf(string digits)
    {
        byte[] baseNumerals = digits[..9].Select(c => (byte)(c - '0')).ToArray();
        byte[] encrypted = Walk(Subkey(KeyLabels.Ff1Cpf), 10, baseNumerals, [], n => n.Distinct().Count() > 1);
        string body = string.Concat(encrypted.Select(n => (char)('0' + n)));
        int first = Modulo11(body, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        int second = Modulo11(body + first, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{body}{first}{second}";
    }

    /// <summary>CNPJ substituto, sem máscara e em maiúsculas, a partir de 14 posições.</summary>
    public static string Cnpj(string chars)
    {
        string upper = chars.ToUpperInvariant();
        bool alphanumeric = upper[..8].Any(char.IsAsciiLetter);
        byte[] root = upper[..8].Select(c => (byte)Base36.IndexOf(c, StringComparison.Ordinal)).ToArray();
        byte[] encrypted = alphanumeric
            ? Walk(Subkey(KeyLabels.Ff1CnpjAlphanumeric), 36, root, [], n => n.Any(x => x >= 10))
            : Walk(Subkey(KeyLabels.Ff1Cnpj), 10, root, [], n => n.Distinct().Count() > 1);
        string body = string.Concat(encrypted.Select(n => Base36[n])) + upper[8..12];
        int first = Modulo11(body, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        int second = Modulo11(body + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{body}{first}{second}";
    }

    /// <summary>Os 7 dígitos finais substitutos de um telefone.</summary>
    /// <param name="ddd">DDD, ou vazio se o número não tem.</param>
    /// <param name="subscriber">Os 8 ou 9 dígitos do assinante.</param>
    public static string PhoneFinal(string ddd, string subscriber)
    {
        string start = subscriber[..^7];
        byte[] tweak = Encoding.ASCII.GetBytes($"{ddd}/{start}");
        byte[] final = subscriber[^7..].Select(c => (byte)(c - '0')).ToArray();
        byte[] encrypted = Ff1(Subkey(KeyLabels.Ff1Phone), 10, final, tweak);
        return string.Concat(encrypted.Select(n => (char)('0' + n)));
    }

    private static byte[] Subkey(ReadOnlySpan<byte> label)
    {
        byte[] subkey = new byte[SubkeyDerivation.SubkeySize];
        SubkeyDerivation.Derive(MasterKey, label, subkey);
        return subkey;
    }

    private static byte[] Walk(byte[] key, int radix, byte[] input, byte[] tweak, Func<byte[], bool> isAllowed)
    {
        byte[] value = Ff1(key, radix, input, tweak);
        while (!isAllowed(value))
        {
            value = Ff1(key, radix, value, tweak);
        }

        return value;
    }

    private static byte[] Ff1(byte[] key, int radix, byte[] input, byte[] tweak)
    {
        var engine = new FpeFf1Engine();
        engine.Init(true, new FpeParameters(new KeyParameter(key), radix, tweak));
        byte[] output = new byte[input.Length];
        engine.ProcessBlock(input, 0, input.Length, output, 0);
        return output;
    }

    /// <summary>Verificador módulo 11 da Receita: valor de cada posição = ASCII − 48.</summary>
    private static int Modulo11(string body, int[] weights)
    {
        int sum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            sum += (body[i] - '0') * weights[i];
        }

        int remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
