using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Formatting;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Cnpj;

public sealed class CnpjFormatTests
{
    internal static readonly Gen<byte[]> NumericRoots =
        Gen.Byte[0, 9].Array[CnpjFormat.RootLength].Where(root => CnpjFormat.IsAllowedNumericRoot(root));

    internal static readonly Gen<byte[]> AlphanumericRoots =
        Gen.Byte[0, 35].Array[CnpjFormat.RootLength].Where(root => CnpjFormat.IsAllowedAlphanumericRoot(root));

    [Theory]
    [InlineData("11.222.333/0001-81", "11222333", "0001", false)]
    [InlineData("11222333000181", "11222333", "0001", false)]
    [InlineData("01.234.567/0001-95", "01234567", "0001", false)]
    [InlineData("12.ABC.345/01DE-35", "12ABC345", "01DE", true)] // exemplo da Receita Federal
    [InlineData("12ABC34501DE35", "12ABC345", "01DE", true)]
    [InlineData("12.abc.345/01de-35", "12ABC345", "01DE", true)]
    public void TryParse_CnpjValido_SeparaRaizEOrdem(string input, string root, string order, bool alphanumeric)
    {
        Assert.True(CnpjFormat.TryParse(input, ValueKind.Text, out ParsedCnpj? cnpj));

        Assert.Equal(root, Chars(cnpj.Root, cnpj.RootRadix));
        Assert.Equal(order, new string(cnpj.Order));
        Assert.Equal(alphanumeric, cnpj.IsAlphanumericRoot);
        Assert.Equal(alphanumeric ? 36 : 10, cnpj.RootRadix);
    }

    [Fact]
    public void TryParse_RaizNumericaComOrdemAlfanumerica_CifraEmBase10()
    {
        // 11222333/AB01: verificadores calculados pela regra ASCII − 48.
        Assert.True(CnpjFormat.TryParse("11222333AB01" + CheckDigits("11222333AB01"), ValueKind.Text, out ParsedCnpj? cnpj));

        Assert.False(cnpj.IsAlphanumericRoot);
        Assert.Equal("AB01", new string(cnpj.Order));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryParse_SemZerosAEsquerda_RecompoeOsZeros(bool asNumber)
    {
        ValueKind kind = asNumber ? ValueKind.Number : ValueKind.Text;

        Assert.True(CnpjFormat.TryParse("1234567000195", kind, out ParsedCnpj? cnpj));

        Assert.Equal("01234567", Chars(cnpj.Root, 10));
        Assert.True(cnpj.Layout.LeadingZerosOmitted);
    }

    [Theory]
    [InlineData("11.222.333/0001-82")] // verificador errado
    [InlineData("12.ABC.345/01DE-34")]
    [InlineData("11.111.111/0001-91")] // raiz com dígitos repetidos
    [InlineData("00.000.000/0001-91")]
    [InlineData("11.222.333/0000-??")] // ordem zerada
    [InlineData("12.ABC.345/01DE-3A")] // verificador com letra
    [InlineData("1.222.333/0001-81")] // máscara sem os zeros à esquerda
    [InlineData("12ABC34501DE3")] // alfanumérico sem uma posição
    [InlineData("112223330001810")] // posições demais
    [InlineData("11.222.333_0001-81")]
    [InlineData("12.ÁBC.345/01DE-35")]
    [InlineData("")]
    public void TryParse_Invalido_DevolveFalso(string input)
    {
        Assert.False(CnpjFormat.TryParse(input, ValueKind.Text, out ParsedCnpj? cnpj));
        Assert.Null(cnpj);
    }

    [Fact]
    public void TryParse_OrdemZeradaComVerificadoresCorretos_DevolveFalso()
    {
        string body = "112223330000";

        Assert.False(CnpjFormat.TryParse(body + CheckDigits(body), ValueKind.Text, out _));
    }

    [Fact]
    public void TryParse_NumeroAlfanumerico_DevolveFalso() =>
        Assert.False(CnpjFormat.TryParse("12ABC34501DE35", ValueKind.Number, out _));

    [Theory]
    [InlineData("11.222.333/0001-81", "98765432", "98.765.432/0001-98")]
    [InlineData("11222333000181", "98765432", "98765432000198")]
    [InlineData("12.ABC.345/01DE-35", "12ABC345", "12.ABC.345/01DE-35")]
    [InlineData("12.abc.345/01de-35", "12ABC345", "12.abc.345/01de-35")]
    [InlineData("12.aBc.345/01DE-35", "12ABC345", "12.ABC.345/01DE-35")]
    public void Format_RecalculaOsVerificadoresEReaplicaFormatoECaixa(string original, string root, string expected)
    {
        Assert.True(CnpjFormat.TryParse(original, ValueKind.Text, out ParsedCnpj? cnpj));
        byte[] numerals = new byte[root.Length];
        Assert.True(Numerals.TryFromChars(root, cnpj.RootRadix, numerals));

        Assert.Equal(expected, CnpjFormat.Format(numerals, cnpj));
    }

    [Fact]
    public void Format_RaizComNumeralForaDaBase_Lanca()
    {
        Assert.True(CnpjFormat.TryParse("11.222.333/0001-81", ValueKind.Text, out ParsedCnpj? cnpj));

        Assert.Throws<ArgumentException>(() => CnpjFormat.Format([1, 2, 3, 4, 5, 6, 7, 10], cnpj));
        Assert.Throws<ArgumentException>(() => CnpjFormat.Format([1, 2, 3], cnpj));
    }

    [Theory]
    [InlineData("11111111", false)]
    [InlineData("11222333", true)]
    public void IsAllowedNumericRoot_ProibeDigitosRepetidos(string root, bool expected) =>
        Assert.Equal(expected, CnpjFormat.IsAllowedNumericRoot(Numerals10(root)));

    [Theory]
    [InlineData("12345678", false)]
    [InlineData("1234567Z", true)]
    [InlineData("AAAAAAAA", true)]
    public void IsAllowedAlphanumericRoot_ExigeLetra(string root, bool expected)
    {
        byte[] numerals = new byte[root.Length];
        Assert.True(Numerals.TryFromChars(root, 36, numerals));

        Assert.Equal(expected, CnpjFormat.IsAllowedAlphanumericRoot(numerals));
    }

    [Fact]
    public void FormatarEInterpretar_RaizNumerica_DevolveARaiz()
    {
        string[] originals = ["11.222.333/0001-81", "11222333000181", "1234567000195"];
        Gen.Select(NumericRoots, Gen.OneOfConst(originals)).Sample((root, original) =>
        {
            Assert.True(CnpjFormat.TryParse(original, ValueKind.Text, out ParsedCnpj? template));
            string formatted = CnpjFormat.Format(root, template);

            Assert.True(CnpjFormat.TryParse(formatted, ValueKind.Text, out ParsedCnpj? parsed));
            Assert.False(parsed.IsAlphanumericRoot);
            Assert.Equal(root, parsed.Root.ToArray());
            Assert.Equal(template.Order.ToArray(), parsed.Order.ToArray());
        });
    }

    [Fact]
    public void FormatarEInterpretar_RaizAlfanumerica_DevolveARaizEACaixa()
    {
        string[] originals = ["12.ABC.345/01DE-35", "12abc34501de35"];
        Gen.Select(AlphanumericRoots, Gen.OneOfConst(originals)).Sample((root, original) =>
        {
            Assert.True(CnpjFormat.TryParse(original, ValueKind.Text, out ParsedCnpj? template));
            string formatted = CnpjFormat.Format(root, template);

            Assert.True(CnpjFormat.TryParse(formatted, ValueKind.Text, out ParsedCnpj? parsed));
            Assert.True(parsed.IsAlphanumericRoot);
            Assert.Equal(root, parsed.Root.ToArray());
            Assert.Equal(original.Length, formatted.Length);
            Assert.Equal(template.LetterCase == LetterCase.Lower, !formatted.Any(char.IsAsciiLetterUpper));
        });
    }

    [Fact]
    public void TrocarUmVerificador_TornaOCnpjInvalido()
    {
        Gen.Select(AlphanumericRoots, Gen.Int[12, 13], Gen.Int[1, 9]).Sample((root, position, delta) =>
        {
            Assert.True(CnpjFormat.TryParse("12ABC34501DE35", ValueKind.Text, out ParsedCnpj? template));
            char[] formatted = CnpjFormat.Format(root, template).ToCharArray();
            formatted[position] = (char)('0' + ((formatted[position] - '0' + delta) % 10));

            Assert.False(CnpjFormat.TryParse(formatted, ValueKind.Text, out _));
        });
    }

    [Fact]
    public void TryParse_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            CnpjFormat.TryParse(input, ValueKind.Text, out _);
            CnpjFormat.TryParse(input, ValueKind.Number, out _);
        });
    }

    private static string CheckDigits(string body)
    {
        byte[] digits = new byte[2];
        CnpjFormat.ComputeCheckDigits(body, digits);
        return $"{digits[0]}{digits[1]}";
    }

    private static byte[] Numerals10(string digits) => digits.Select(c => (byte)(c - '0')).ToArray();

    private static string Chars(ReadOnlySpan<byte> numerals, int radix)
    {
        char[] chars = new char[numerals.Length];
        Numerals.ToChars(numerals, radix, chars);
        return new string(chars);
    }
}
