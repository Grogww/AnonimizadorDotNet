using System.Buffers;
using AnonimizadorDotNet.Documents.Formatting;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Formatting;

public sealed class TextLayoutTests
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> Mask = SearchValues.Create(".-/ ");

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData(" 529 982 247 25 ", "52998224725")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    public void TryCapture_SeparaOsCaracteresSignificativos(string input, string expected)
    {
        char[] destination = new char[32];

        Assert.True(TextLayout.TryCapture(input, Digits, Mask, ValueKind.Text, destination, out int written, out TextLayout? layout));

        Assert.Equal(expected, new string(destination, 0, written));
        Assert.Equal(expected.Length, layout.SlotCount);
        Assert.Equal(input.Length != expected.Length, layout.HasSeparators);
    }

    [Fact]
    public void Apply_ReaplicaAMascaraANovosCaracteres()
    {
        char[] destination = new char[32];
        Assert.True(TextLayout.TryCapture("529.982.247-25", Digits, Mask, ValueKind.Text, destination, out _, out TextLayout? layout));

        Assert.Equal("123.456.789-09", layout.Apply("12345678909"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("...-")]
    [InlineData("529_982")]
    [InlineData("529982a")]
    public void TryCapture_EntradaInvalida_DevolveFalso(string input)
    {
        char[] destination = new char[32];

        Assert.False(TextLayout.TryCapture(input, Digits, Mask, ValueKind.Text, destination, out int written, out TextLayout? layout));
        Assert.Equal(0, written);
        Assert.Null(layout);
    }

    [Fact]
    public void TryCapture_MaisCaracteresQueODestino_DevolveFalso()
    {
        char[] destination = new char[4];

        Assert.False(TextLayout.TryCapture("12345", Digits, Mask, ValueKind.Text, destination, out int written, out _));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryCapture_Numero_NaoAceitaSeparadoresEOmiteZeros()
    {
        char[] destination = new char[32];

        Assert.False(TextLayout.TryCapture("529.982", Digits, Mask, ValueKind.Number, destination, out _, out _));
        Assert.True(TextLayout.TryCapture("1234567890", Digits, Mask, ValueKind.Number, destination, out _, out TextLayout? layout));

        Assert.True(layout.LeadingZerosOmitted);
        Assert.Equal(ValueKind.Number, layout.Kind);
        Assert.Equal("1234567890", layout.Apply("01234567890"));
        Assert.Equal("98765432100", layout.Apply("98765432100"));
        Assert.Equal("0", layout.Apply("00000"));
    }

    [Fact]
    public void OmittingLeadingZeros_TextoSemMascara_RemoveZerosNaSaida()
    {
        char[] destination = new char[32];
        Assert.True(TextLayout.TryCapture("1234567890", Digits, Mask, ValueKind.Text, destination, out _, out TextLayout? layout));

        TextLayout omitting = layout.OmittingLeadingZeros();

        Assert.False(layout.LeadingZerosOmitted);
        Assert.True(omitting.LeadingZerosOmitted);
        Assert.Equal(ValueKind.Text, omitting.Kind);
        Assert.Equal("123", omitting.Apply("00000000123"));
    }

    [Fact]
    public void OmittingLeadingZeros_ComMascara_Lanca()
    {
        char[] destination = new char[32];
        Assert.True(TextLayout.TryCapture("123.456", Digits, Mask, ValueKind.Text, destination, out _, out TextLayout? layout));

        Assert.Throws<InvalidOperationException>(layout.OmittingLeadingZeros);
    }

    [Fact]
    public void Apply_QuantidadeDiferente_LancaSemOValor()
    {
        char[] destination = new char[32];
        Assert.True(TextLayout.TryCapture("529.982.247-25", Digits, Mask, ValueKind.Text, destination, out _, out TextLayout? layout));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => layout.Apply("1234"));
        Assert.DoesNotContain("1234", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NaoExpoeOValorOriginal()
    {
        char[] destination = new char[32];
        Assert.True(TextLayout.TryCapture("529.982.247-25", Digits, Mask, ValueKind.Text, destination, out _, out TextLayout? layout));

        Assert.DoesNotContain("529", layout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CapturarEReaplicar_DevolveAEntrada()
    {
        Gen<string> inputs = Gen.Char["0123456789.-/ "].Array[1, 30]
            .Where(chars => chars.Any(char.IsAsciiDigit))
            .Select(chars => new string(chars));

        inputs.Sample(input =>
        {
            char[] destination = new char[input.Length];
            Assert.True(TextLayout.TryCapture(input, Digits, Mask, ValueKind.Text, destination, out int written, out TextLayout? layout));
            Assert.Equal(input, layout.Apply(destination.AsSpan(0, written)));
        });
    }
}
