using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Cpf;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Tests.Documents.Cpf;
using AnonimizadorDotNet.Transformation.Cpf;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Transformation.Cpf;

public sealed class CpfTransformerTests : IDisposable
{
    private readonly KeyRing _keys = new(ReferenceSubstitutes.MasterKey);
    private readonly CpfTransformer _transformer;

    public CpfTransformerTests() => _transformer = new CpfTransformer(_keys);

    /// <summary>CPFs válidos, com ou sem máscara.</summary>
    private static readonly Gen<string> ValidCpfs =
        from baseNumerals in CpfFormatTests.AllowedBases
        from masked in Gen.Bool
        select Compose(baseNumerals, masked);

    public void Dispose()
    {
        _transformer.Dispose();
        _keys.Dispose();
    }

    /// <summary>
    /// Valores dourados: fixam rótulo, tweak e <i>cycle-walking</i> do CPF. Se quebrarem, todos os CPFs
    /// substitutos mudaram. Conferidos contra <see cref="ReferenceSubstitutes"/>, calculado com o BouncyCastle.
    /// </summary>
    [Theory]
    [InlineData("529.982.247-25", "680.133.296-15")]
    [InlineData("52998224725", "68013329615")]
    [InlineData("111.444.777-35", "645.674.055-01")]
    [InlineData("012.345.678-90", "903.153.170-78")]
    public void TryTransform_ChaveConhecida_ProduzSubstitutoFixo(string input, string expected)
    {
        Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

        Assert.Equal(expected, substitute);
        Assert.Equal(ReferenceSubstitutes.Cpf(Digits(input)), Digits(substitute));
    }

    [Fact]
    public void TryTransform_IgualAoCalculoDeReferencia()
    {
        ValidCpfs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));
            Assert.Equal(ReferenceSubstitutes.Cpf(Digits(input)), Digits(substitute));
        }, iter: 300);
    }

    [Fact]
    public void TryTransform_SaiCpfValidoNaMesmaRepresentacao()
    {
        ValidCpfs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

            Assert.True(CpfFormat.TryParse(substitute, ValueKind.Text, out _));
            Assert.Equal(Mask(input), Mask(substitute));
        });
    }

    [Fact]
    public void TryReverse_DevolveOOriginal()
    {
        ValidCpfs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));
            Assert.True(_transformer.TryReverse(substitute, ValueKind.Text, out string? original));

            Assert.Equal(input, original);
        });
    }

    [Fact]
    public void TryTransform_ComoNumero_SaiNumero()
    {
        CpfFormatTests.AllowedBases.Sample(baseNumerals =>
        {
            string number = Compose(baseNumerals, masked: false).TrimStart('0');
            Assert.True(_transformer.TryTransform(number, ValueKind.Number, out string? substitute));

            Assert.True(long.TryParse(substitute, out long parsed));
            Assert.Equal(parsed.ToString(System.Globalization.CultureInfo.InvariantCulture), substitute);
            Assert.True(_transformer.TryReverse(substitute, ValueKind.Number, out string? original));
            Assert.Equal(number, original);
        });
    }

    [Fact]
    public void TryTransform_OutraChave_OutroSubstituto()
    {
        byte[] otherKey = ReferenceSubstitutes.MasterKey.Select(b => (byte)(b ^ 0xFF)).ToArray();
        using var otherKeys = new KeyRing(otherKey);
        using var other = new CpfTransformer(otherKeys);

        ValidCpfs.Sample(input =>
        {
            Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? first));
            Assert.True(other.TryTransform(input, ValueKind.Text, out string? second));

            Assert.NotEqual(first, second);
        }, iter: 200);
    }

    [Fact]
    public void TryTransform_ValoresDistintos_NuncaColidem()
    {
        var substitutes = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        for (long value = 1; count < 50_000; value += 19_997)
        {
            byte[] baseNumerals = value.ToString("D9", System.Globalization.CultureInfo.InvariantCulture)
                .Select(c => (byte)(c - '0')).ToArray();
            if (!CpfFormat.IsAllowedBase(baseNumerals))
            {
                continue;
            }

            Assert.True(_transformer.TryTransform(Compose(baseNumerals, masked: false), ValueKind.Text, out string? substitute));
            Assert.True(substitutes.Add(substitute));
            count++;
        }
    }

    [Theory]
    [InlineData("529.982.247-26")]
    [InlineData("111.111.111-11")]
    [InlineData("12345")]
    [InlineData("")]
    public void TryTransform_Invalido_DevolveFalso(string input)
    {
        Assert.False(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));
        Assert.Null(substitute);
    }

    [Fact]
    public void TryTransform_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            _transformer.TryTransform(input, ValueKind.Text, out _);
            _transformer.TryTransform(input, ValueKind.Number, out _);
        });
    }

    [Fact]
    public void DepoisDoDescarte_Lanca()
    {
        var transformer = new CpfTransformer(_keys);
        transformer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transformer.TryTransform("529.982.247-25", ValueKind.Text, out _));
    }

    private static string Compose(byte[] baseNumerals, bool masked)
    {
        byte[] checkDigits = new byte[2];
        CpfFormat.ComputeCheckDigits(baseNumerals, checkDigits);
        string digits = string.Concat(baseNumerals.Concat(checkDigits).Select(n => (char)('0' + n)));
        return masked ? $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}" : digits;
    }

    private static string Digits(string text) => string.Concat(text.Where(char.IsAsciiDigit)).PadLeft(11, '0');

    private static string Mask(string text) => string.Concat(text.Select(c => char.IsAsciiDigit(c) ? '#' : c));
}
