using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Transformation.Cnpj;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Transformation.Cnpj;

public sealed class CnpjTransformerTests : IDisposable
{
    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly KeyRing _keys = new(ReferenceSubstitutes.MasterKey);
    private readonly CnpjTransformer _transformer;

    public CnpjTransformerTests() => _transformer = new CnpjTransformer(_keys);

    private static readonly Gen<string> Orders =
        Gen.Char["0123456789"].Array[4].Select(chars => new string(chars)).Where(order => order != "0000");

    private static readonly Gen<string> NumericCnpjs =
        from root in Gen.Char["0123456789"].Array[8].Where(r => r.Distinct().Count() > 1)
        from order in Orders
        select Compose(new string(root) + order);

    private static readonly Gen<string> AlphanumericCnpjs =
        from root in Gen.Char[Base36].Array[8].Where(r => r.Any(char.IsAsciiLetter))
        from order in Gen.Char[Base36].Array[4].Select(chars => new string(chars)).Where(order => order != "0000")
        select Compose(new string(root) + order);

    public void Dispose()
    {
        _transformer.Dispose();
        _keys.Dispose();
    }

    /// <summary>
    /// Valores dourados: fixam rótulos, tweak e <i>cycle-walking</i> do CNPJ nos dois domínios. Conferidos
    /// contra <see cref="ReferenceSubstitutes"/>, calculado com o BouncyCastle.
    /// </summary>
    [Theory]
    [InlineData("11.222.333/0001-81", "34.410.014/0001-03")]
    [InlineData("11222333000181", "34410014000103")]
    [InlineData("12.ABC.345/01DE-35", "71.99R.CAS/01DE-29")]
    [InlineData("12abc34501de35", "7199rcas01de29")]
    public void TryTransform_ChaveConhecida_ProduzSubstitutoFixo(string input, string expected)
    {
        Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

        Assert.Equal(expected, substitute);
        Assert.Equal(ReferenceSubstitutes.Cnpj(Significant(input)), Significant(substitute).ToUpperInvariant());
    }

    [Fact]
    public void TryTransform_IgualAoCalculoDeReferencia()
    {
        Gen.OneOf(NumericCnpjs, AlphanumericCnpjs).Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));
            Assert.Equal(ReferenceSubstitutes.Cnpj(Significant(input)), Significant(substitute));
        }, iter: 300);
    }

    [Fact]
    public void TryTransform_Numerico_SaiNumericoValidoComAMesmaOrdem()
    {
        NumericCnpjs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

            Assert.True(CnpjFormat.TryParse(substitute, ValueKind.Text, out ParsedCnpj? parsed));
            Assert.False(parsed.IsAlphanumericRoot);
            Assert.Equal(Significant(input)[8..12], Significant(substitute)[8..12]);
            Assert.Equal(Mask(input), Mask(substitute));
        });
    }

    [Fact]
    public void TryTransform_Alfanumerico_SaiComLetraNaRaiz()
    {
        AlphanumericCnpjs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

            Assert.True(CnpjFormat.TryParse(substitute, ValueKind.Text, out ParsedCnpj? parsed));
            Assert.True(parsed.IsAlphanumericRoot);
            Assert.Equal(Significant(input)[8..12], Significant(substitute)[8..12]);
        });
    }

    [Fact]
    public void TryTransform_MatrizEFiliais_MantemAMesmaRaiz()
    {
        Assert.True(_transformer.TryTransform("11.222.333/0001-81", ValueKind.Text, out string? matriz));
        Assert.True(_transformer.TryTransform("11.222.333/0002-62", ValueKind.Text, out string? filial));

        Assert.Equal(matriz[..10], filial[..10]);
        Assert.EndsWith("/0001", matriz[..15], StringComparison.Ordinal);
        Assert.EndsWith("/0002", filial[..15], StringComparison.Ordinal);
    }

    [Fact]
    public void TryTransform_Minusculas_SaiEmMinusculas()
    {
        Assert.True(_transformer.TryTransform("12abc34501de35", ValueKind.Text, out string? substitute));

        Assert.Equal(substitute.ToLowerInvariant(), substitute);
    }

    [Fact]
    public void TryReverse_DevolveOOriginal()
    {
        Gen.OneOf(NumericCnpjs, AlphanumericCnpjs).Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));
            Assert.True(_transformer.TryReverse(substitute, ValueKind.Text, out string? original));

            Assert.Equal(input, original);
        });
    }

    [Fact]
    public void TryTransform_ComoNumero_SaiNumero()
    {
        // Sem o zero à esquerda, como fica num campo bigint.
        string number = Compose("012223330001").TrimStart('0');
        Assert.True(_transformer.TryTransform(number, ValueKind.Number, out string? substitute));

        Assert.True(long.TryParse(substitute, out _));
        Assert.DoesNotContain(substitute, c => !char.IsAsciiDigit(c));
        Assert.True(_transformer.TryReverse(substitute, ValueKind.Number, out string? original));
        Assert.Equal(number, original);
    }

    [Fact]
    public void TryTransform_RaizesDistintas_NuncaColidem()
    {
        var numeric = new HashSet<string>(StringComparer.Ordinal);
        var alphanumeric = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i <= 25_000; i++)
        {
            string digits = (i * 3_989L).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            if (digits.Distinct().Count() > 1)
            {
                Assert.True(_transformer.TryTransform(Compose(digits + "0001"), ValueKind.Text, out string? n));
                Assert.True(numeric.Add(n[..8]));
            }

            string letters = "A" + digits[1..];
            Assert.True(_transformer.TryTransform(Compose(letters + "0001"), ValueKind.Text, out string? a));
            Assert.True(alphanumeric.Add(a[..8]));
        }
    }

    [Theory]
    [InlineData("11.222.333/0001-82")]
    [InlineData("00.000.000/0000-00")]
    [InlineData("11.222.333/0000-00")]
    [InlineData("")]
    public void TryTransform_Invalido_DevolveFalso(string input) =>
        Assert.False(_transformer.TryTransform(input, ValueKind.Text, out _));

    [Fact]
    public void TryTransform_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            _transformer.TryTransform(input, ValueKind.Text, out _);
            _transformer.TryTransform(input, ValueKind.Number, out _);
        });
    }

    /// <summary>Acrescenta os verificadores a 12 posições, sem máscara.</summary>
    private static string Compose(string body)
    {
        byte[] checkDigits = new byte[2];
        CnpjFormat.ComputeCheckDigits(body, checkDigits);
        return body + (char)('0' + checkDigits[0]) + (char)('0' + checkDigits[1]);
    }

    private static string Significant(string text) =>
        string.Concat(text.Where(char.IsAsciiLetterOrDigit)).PadLeft(14, '0');

    private static string Mask(string text) => string.Concat(text.Select(c => char.IsAsciiLetterOrDigit(c) ? '#' : c));
}
