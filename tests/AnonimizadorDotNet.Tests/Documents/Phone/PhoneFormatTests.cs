using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Documents.Phone;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Phone;

public sealed class PhoneFormatTests
{
    [Theory]
    [InlineData("(11) 91234-5678", "11", true, "11 9 1", "2345678")]
    [InlineData("11912345678", "11", true, "11 9 1", "2345678")]
    [InlineData("+55 (11) 91234-5678", "11", true, "55 11 9 1", "2345678")]
    [InlineData("+5511912345678", "11", true, "55 11 9 1", "2345678")]
    [InlineData("55 11 91234 5678", "11", true, "55 11 9 1", "2345678")]
    [InlineData("011 91234-5678", "11", true, "0 11 9 1", "2345678")]
    [InlineData("(21) 3456-7890", "21", false, "21 3", "4567890")]
    [InlineData("2134567890", "21", false, "21 3", "4567890")]
    [InlineData("021 3456-7890", "21", false, "0 21 3", "4567890")]
    [InlineData("+55 21 3456.7890", "21", false, "55 21 3", "4567890")]
    [InlineData("(55) 99876-5432", "55", true, "55 9 9", "8765432")] // DDD 55 (Santa Maria) sem código do país
    [InlineData("91234-5678", null, true, "9 1", "2345678")]
    [InlineData("3456-7890", null, false, "3", "4567890")]
    [InlineData("(31) 8765-4321", "31", false, "31 8", "7654321")] // celular antigo, sem o 9
    public void TryParse_TelefoneValido_SeparaPreservadoECifrado(
        string input, string? ddd, bool isMobile, string preserved, string subscriber)
    {
        Assert.True(PhoneFormat.TryParse(input, ValueKind.Text, out ParsedPhone? phone));

        Assert.Equal(ddd, phone.Ddd);
        Assert.Equal(isMobile, phone.IsMobile);
        Assert.Equal(preserved.Replace(" ", "", StringComparison.Ordinal), new string(phone.Preserved));
        Assert.Equal(subscriber, string.Concat(phone.Subscriber.ToArray().Select(n => (char)('0' + n))));
    }

    [Theory]
    [InlineData("(10) 91234-5678")] // DDD inexistente
    [InlineData("(20) 3456-7890")]
    [InlineData("(11) 81234-5678")] // 9 dígitos sem começar por 9
    [InlineData("(11) 1234-5678")] // fixo começando por 1
    [InlineData("(11) 0234-5678")]
    [InlineData("1234567")] // dígitos de menos
    [InlineData("12345678901234")] // dígitos demais
    [InlineData("+44 20 7946 0958")] // outro país
    [InlineData("+11 3456-7890")] // + sem código do país
    [InlineData("11 +55 3456-7890")]
    [InlineData("++55 11 3456-7890")]
    [InlineData("0800 123 4567")] // DDD "80" inexistente
    [InlineData("(11) 91234/5678")]
    [InlineData("")]
    public void TryParse_Invalido_DevolveFalso(string input)
    {
        Assert.False(PhoneFormat.TryParse(input, ValueKind.Text, out ParsedPhone? phone));
        Assert.Null(phone);
    }

    [Fact]
    public void TryParse_Numero_SemSeparadores()
    {
        Assert.True(PhoneFormat.TryParse("5511912345678", ValueKind.Number, out ParsedPhone? phone));
        Assert.Equal("11", phone.Ddd);
        Assert.False(PhoneFormat.TryParse("(11) 91234-5678", ValueKind.Number, out _));
    }

    [Theory]
    [InlineData("(11) 91234-5678", "(11) 91765-4321")]
    [InlineData("+55 (11) 91234-5678", "+55 (11) 91765-4321")]
    [InlineData("11912345678", "11917654321")]
    [InlineData("(21) 3456-7890", "(21) 3765-4321")]
    [InlineData("3456-7890", "3765-4321")]
    public void Format_PreservaPrefixoEReaplicaAMascara(string original, string expected)
    {
        Assert.True(PhoneFormat.TryParse(original, ValueKind.Text, out ParsedPhone? phone));

        Assert.Equal(expected, PhoneFormat.Format([7, 6, 5, 4, 3, 2, 1], phone));
    }

    [Fact]
    public void Format_TamanhoErrado_Lanca()
    {
        Assert.True(PhoneFormat.TryParse("(11) 91234-5678", ValueKind.Text, out ParsedPhone? phone));

        Assert.Throws<ArgumentException>(() => PhoneFormat.Format([1, 2, 3], phone));
    }

    [Fact]
    public void ValidDdds_Tem67()
    {
        Assert.Equal(67, PhoneFormat.ValidDdds.Count);
        Assert.All(PhoneFormat.ValidDdds, ddd => Assert.Matches("^[1-9][1-9]$", ddd));
    }

    [Fact]
    public void FormatarEInterpretar_MantemTipoDDDEPreservado()
    {
        string[] originals = ["(11) 91234-5678", "+55 21 3456-7890", "021 3456-7890", "91234-5678", "(31) 8765-4321"];
        Gen.Select(Gen.Byte[0, 9].Array[PhoneFormat.SubscriberLength], Gen.OneOfConst(originals)).Sample((subscriber, original) =>
        {
            Assert.True(PhoneFormat.TryParse(original, ValueKind.Text, out ParsedPhone? template));
            string formatted = PhoneFormat.Format(subscriber, template);

            Assert.True(PhoneFormat.TryParse(formatted, ValueKind.Text, out ParsedPhone? parsed));
            Assert.Equal(original.Length, formatted.Length);
            Assert.Equal(template.Ddd, parsed.Ddd);
            Assert.Equal(template.IsMobile, parsed.IsMobile);
            Assert.Equal(template.Preserved.ToArray(), parsed.Preserved.ToArray());
            Assert.Equal(subscriber, parsed.Subscriber.ToArray());
        });
    }

    [Fact]
    public void TryParse_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            PhoneFormat.TryParse(input, ValueKind.Text, out _);
            PhoneFormat.TryParse(input, ValueKind.Number, out _);
        });
    }
}
