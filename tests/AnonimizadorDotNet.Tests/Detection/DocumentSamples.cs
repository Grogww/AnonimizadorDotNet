using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Cpf;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection;

/// <summary>Documentos válidos para os testes de detecção, montados com os verificadores corretos.</summary>
internal static class DocumentSamples
{
    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>CPF sem máscara.</summary>
    public static readonly Gen<string> Cpfs =
        Gen.Char["0123456789"].Array[9].Where(b => b.Distinct().Count() > 1).Select(b => Cpf(new string(b)));

    /// <summary>CNPJ só de dígitos, sem máscara.</summary>
    public static readonly Gen<string> NumericCnpjs =
        from root in Gen.Char["0123456789"].Array[8].Where(r => r.Distinct().Count() > 1)
        from order in Gen.Char["0123456789"].Array[4].Where(o => new string(o) != "0000")
        select Cnpj(new string(root) + new string(order));

    /// <summary>CNPJ alfanumérico (ao menos uma letra na raiz), sem máscara, em maiúsculas.</summary>
    public static readonly Gen<string> AlphanumericCnpjs =
        from root in Gen.Char[Base36].Array[8].Where(r => r.Any(char.IsAsciiLetter))
        from order in Gen.Char[Base36].Array[4].Where(o => new string(o) != "0000")
        select Cnpj(new string(root) + new string(order));

    /// <summary>Completa uma base de 9 dígitos com os verificadores.</summary>
    public static string Cpf(string baseDigits)
    {
        byte[] numerals = baseDigits.Select(c => (byte)(c - '0')).ToArray();
        Span<byte> checkDigits = stackalloc byte[2];
        CpfFormat.ComputeCheckDigits(numerals, checkDigits);
        return $"{baseDigits}{checkDigits[0]}{checkDigits[1]}";
    }

    /// <summary>Completa um corpo de 12 posições (dígitos e letras maiúsculas) com os verificadores.</summary>
    public static string Cnpj(string body)
    {
        Span<byte> checkDigits = stackalloc byte[2];
        CnpjFormat.ComputeCheckDigits(body, checkDigits);
        return $"{body}{checkDigits[0]}{checkDigits[1]}";
    }

    /// <summary>CPF com máscara: <c>000.000.000-00</c>.</summary>
    public static string MaskCpf(string cpf) => $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

    /// <summary>CNPJ com máscara: <c>00.000.000/0000-00</c>.</summary>
    public static string MaskCnpj(string cnpj) => $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";

    /// <summary>O mesmo valor com o último verificador trocado (verificador errado).</summary>
    public static string BreakCheckDigit(string value) =>
        value[..^1] + (char)('0' + ((value[^1] - '0' + 1) % 10));
}
