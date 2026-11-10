using AnonimizadorDotNet.Documents.Cpf;
using AnonimizadorDotNet.Documents.Formatting;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Cpf;

public sealed class CpfFormatTests
{
    /// <summary>Bases de 9 dígitos que não são todos iguais.</summary>
    internal static readonly Gen<byte[]> AllowedBases =
        Gen.Byte[0, 9].Array[CpfFormat.BaseLength].Where(numerals => CpfFormat.IsAllowedBase(numerals));

    [Theory]
    [InlineData("529.982.247-25", "529982247")]
    [InlineData("52998224725", "529982247")]
    [InlineData("111.444.777-35", "111444777")]
    [InlineData("012.345.678-90", "012345678")]
    [InlineData("01234567890", "012345678")]
    [InlineData(" 529.982.247-25 ", "529982247")]
    [InlineData("529 982 247 25", "529982247")]
    public void TryParse_CpfValido_DevolveABase(string input, string expectedBase)
    {
        Assert.True(CpfFormat.TryParse(input, ValueKind.Text, out ParsedCpf? cpf));

        Assert.Equal(expectedBase, Digits(cpf.Base));
        Assert.False(cpf.Layout.LeadingZerosOmitted);
    }

    [Theory]
    [InlineData("1234567890", false)]
    [InlineData("1234567890", true)]
    public void TryParse_SemZerosAEsquerda_RecompoeOsZeros(string input, bool asNumber)
    {
        ValueKind kind = asNumber ? ValueKind.Number : ValueKind.Text;
        Assert.True(CpfFormat.TryParse(input, kind, out ParsedCpf? cpf));

        Assert.Equal("012345678", Digits(cpf.Base));
        Assert.True(cpf.Layout.LeadingZerosOmitted);
        Assert.Equal(kind, cpf.Layout.Kind);
    }

    [Theory]
    [InlineData("529.982.247-26")] // segundo verificador errado
    [InlineData("529.982.247-15")] // primeiro verificador errado
    [InlineData("111.111.111-11")] // dígitos repetidos com verificadores corretos
    [InlineData("000.000.000-00")]
    [InlineData("99999999999")]
    [InlineData("12.345.678-90")] // máscara sem os zeros à esquerda
    [InlineData("529982247250")] // dígitos demais
    [InlineData("529.982.247/25")] // separador de CNPJ
    [InlineData("529.982.247-2a")]
    [InlineData("")]
    [InlineData("...-")]
    public void TryParse_Invalido_DevolveFalso(string input)
    {
        Assert.False(CpfFormat.TryParse(input, ValueKind.Text, out ParsedCpf? cpf));
        Assert.Null(cpf);
    }

    [Fact]
    public void TryParse_NumeroComMascara_DevolveFalso() =>
        Assert.False(CpfFormat.TryParse("529.982.247-25", ValueKind.Number, out _));

    [Theory]
    [InlineData("529.982.247-25", "123.456.789-09")]
    [InlineData("52998224725", "12345678909")]
    [InlineData("529 982 247 25", "123 456 789 09")]
    public void Format_RecalculaOsVerificadoresEReaplicaAMascara(string original, string expected)
    {
        Assert.True(CpfFormat.TryParse(original, ValueKind.Text, out ParsedCpf? cpf));

        Assert.Equal(expected, CpfFormat.Format([1, 2, 3, 4, 5, 6, 7, 8, 9], cpf.Layout));
    }

    [Fact]
    public void Format_SemZerosAEsquerda_SaiSemZeros()
    {
        Assert.True(CpfFormat.TryParse("52998224725", ValueKind.Number, out ParsedCpf? cpf));

        Assert.Equal("1234567890", CpfFormat.Format([0, 1, 2, 3, 4, 5, 6, 7, 8], cpf.Layout));
    }

    [Fact]
    public void Format_BaseComTamanhoErrado_Lanca()
    {
        Assert.True(CpfFormat.TryParse("52998224725", ValueKind.Text, out ParsedCpf? cpf));

        Assert.Throws<ArgumentException>(() => CpfFormat.Format([1, 2, 3], cpf.Layout));
    }

    [Theory]
    [InlineData("000000000", false)]
    [InlineData("777777777", false)]
    [InlineData("000000001", true)]
    [InlineData("529982247", true)]
    public void IsAllowedBase_ProibeDigitosRepetidos(string baseDigits, bool expected) =>
        Assert.Equal(expected, CpfFormat.IsAllowedBase(baseDigits.Select(c => (byte)(c - '0')).ToArray()));

    [Fact]
    public void FormatarEInterpretar_DevolveABase_ComQualquerMascara()
    {
        string[] originals = ["529.982.247-25", "52998224725", "1234567890"];
        Gen.Select(AllowedBases, Gen.OneOfConst(originals)).Sample((baseNumerals, original) =>
        {
            Assert.True(CpfFormat.TryParse(original, ValueKind.Text, out ParsedCpf? template));
            string formatted = CpfFormat.Format(baseNumerals, template.Layout);

            Assert.True(CpfFormat.TryParse(formatted, ValueKind.Text, out ParsedCpf? parsed));
            Assert.Equal(baseNumerals, parsed.Base.ToArray());
        });
    }

    [Fact]
    public void TrocarUmVerificador_TornaOCpfInvalido()
    {
        Gen.Select(AllowedBases, Gen.Int[9, 10], Gen.Int[1, 9]).Sample((baseNumerals, position, delta) =>
        {
            Assert.True(CpfFormat.TryParse("52998224725", ValueKind.Text, out ParsedCpf? template));
            char[] formatted = CpfFormat.Format(baseNumerals, template.Layout).ToCharArray();
            formatted[position] = (char)('0' + ((formatted[position] - '0' + delta) % 10));

            Assert.False(CpfFormat.TryParse(formatted, ValueKind.Text, out _));
        });
    }

    [Fact]
    public void TryParse_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            CpfFormat.TryParse(input, ValueKind.Text, out _);
            CpfFormat.TryParse(input, ValueKind.Number, out _);
        });
    }

    private static string Digits(ReadOnlySpan<byte> numerals) =>
        string.Concat(numerals.ToArray().Select(n => (char)('0' + n)));
}
