using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Documents.Phone;
using AnonimizadorDotNet.Transformation.Phone;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Transformation.Phone;

public sealed class PhoneTransformerTests : IDisposable
{
    private readonly KeyRing _keys = new(ReferenceSubstitutes.MasterKey);
    private readonly PhoneTransformer _transformer;

    public PhoneTransformerTests() => _transformer = new PhoneTransformer(_keys);

    private static readonly Gen<string> Ddds = Gen.OneOfConst(PhoneFormat.ValidDdds.Order(StringComparer.Ordinal).ToArray());

    private static readonly Gen<string> Subscribers = Gen.OneOf(
        Gen.Char["0123456789"].Array[8].Select(rest => "9" + new string(rest)),
        from first in Gen.Char["23456789"]
        from rest in Gen.Char["0123456789"].Array[7]
        select first + new string(rest));

    private static readonly Gen<(string Ddd, string Subscriber, string Text)> Phones =
        from ddd in Ddds
        from subscriber in Subscribers
        from style in Gen.Int[0, 3]
        select (ddd, subscriber, style switch
        {
            0 => $"({ddd}) {subscriber[..^4]}-{subscriber[^4..]}",
            1 => $"+55 {ddd} {subscriber}",
            2 => ddd + subscriber,
            _ => $"0{ddd} {subscriber}",
        });

    public void Dispose()
    {
        _transformer.Dispose();
        _keys.Dispose();
    }

    /// <summary>
    /// Valores dourados: fixam rótulo e tweak (<c>DDD/início do assinante</c>) do telefone. Conferidos contra
    /// <see cref="ReferenceSubstitutes"/>, calculado com o BouncyCastle.
    /// </summary>
    [Theory]
    [InlineData("(11) 98765-4321", "11", "987654321", "(11) 98759-4010")]
    [InlineData("+55 21 3456-7890", "21", "34567890", "+55 21 3227-2226")]
    [InlineData("98765-4321", "", "987654321", "98281-6663")]
    [InlineData("1134567890", "11", "34567890", "1139459720")]
    public void TryTransform_ChaveConhecida_ProduzSubstitutoFixo(string input, string ddd, string subscriber, string expected)
    {
        Assert.True(_transformer.TryTransform(input, ValueKind.Text, out string? substitute));

        Assert.Equal(expected, substitute);
        Assert.EndsWith(ReferenceSubstitutes.PhoneFinal(ddd, subscriber), Digits(substitute), StringComparison.Ordinal);
    }

    [Fact]
    public void TryTransform_IgualAoCalculoDeReferencia()
    {
        Phones.Sample(phone =>
        {
            Assert.True(_transformer.TryTransform(phone.Text, ValueKind.Text, out string? substitute));
            Assert.EndsWith(ReferenceSubstitutes.PhoneFinal(phone.Ddd, phone.Subscriber), Digits(substitute), StringComparison.Ordinal);
        }, iter: 300);
    }

    [Fact]
    public void TryTransform_PreservaPrefixoDDDInicioDoAssinanteEMascara()
    {
        Phones.Sample(phone =>
        {
            Assert.True(_transformer.TryTransform(phone.Text, ValueKind.Text, out string? substitute));

            Assert.True(PhoneFormat.TryParse(substitute, ValueKind.Text, out _));
            Assert.Equal(Mask(phone.Text), Mask(substitute));
            string preserved = Digits(phone.Text)[..^7];
            Assert.StartsWith(preserved, Digits(substitute), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TryTransform_CodigoDoPaisNaoMudaOFinal()
    {
        Assert.True(_transformer.TryTransform("(11) 98765-4321", ValueKind.Text, out string? local));
        Assert.True(_transformer.TryTransform("+55 11 98765-4321", ValueKind.Text, out string? international));
        Assert.True(_transformer.TryTransform("011 98765-4321", ValueKind.Text, out string? trunk));

        Assert.Equal(Digits(local)[^7..], Digits(international)[^7..]);
        Assert.Equal(Digits(local)[^7..], Digits(trunk)[^7..]);
    }

    [Fact]
    public void TryTransform_MesmoFinalEmDDDsOuFaixasDiferentes_FinaisIndependentes()
    {
        Assert.True(_transformer.TryTransform("(11) 98765-4321", ValueKind.Text, out string? sp));
        Assert.True(_transformer.TryTransform("(21) 98765-4321", ValueKind.Text, out string? rj));
        Assert.True(_transformer.TryTransform("(11) 97765-4321", ValueKind.Text, out string? otherRange));
        Assert.True(_transformer.TryTransform("98765-4321", ValueKind.Text, out string? noDdd));

        string[] finals = [Digits(sp)[^7..], Digits(rj)[^7..], Digits(otherRange)[^7..], Digits(noDdd)[^7..]];
        Assert.Equal(finals.Length, finals.Distinct().Count());
    }

    [Fact]
    public void TryReverse_DevolveOOriginal()
    {
        Phones.Sample(phone =>
        {
            Assert.True(_transformer.TryTransform(phone.Text, ValueKind.Text, out string? substitute));
            Assert.True(_transformer.TryReverse(substitute, ValueKind.Text, out string? original));

            Assert.Equal(phone.Text, original);
        });
    }

    [Fact]
    public void TryTransform_ComoNumero_SaiNumero()
    {
        Assert.True(_transformer.TryTransform("5511987654321", ValueKind.Number, out string? substitute));

        Assert.True(long.TryParse(substitute, out _));
        Assert.StartsWith("551198", substitute, StringComparison.Ordinal);
    }

    [Fact]
    public void TryTransform_FinaisDistintos_NuncaColidem()
    {
        var substitutes = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 50_000; i++)
        {
            string final = (i * 199L).ToString("D7", System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(_transformer.TryTransform("1198" + final, ValueKind.Text, out string? substitute));
            Assert.True(substitutes.Add(substitute));
        }
    }

    [Theory]
    [InlineData("(10) 98765-4321")]
    [InlineData("(11) 88765-4321")]
    [InlineData("1234-5678")]
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

    private static string Digits(string text) => string.Concat(text.Where(char.IsAsciiDigit));

    private static string Mask(string text) => string.Concat(text.Select(c => char.IsAsciiDigit(c) ? '#' : c));
}
