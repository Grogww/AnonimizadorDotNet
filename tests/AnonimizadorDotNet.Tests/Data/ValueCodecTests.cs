using AnonimizadorDotNet.Data;

namespace AnonimizadorDotNet.Tests.Data;

public sealed class ValueCodecTests
{
    public static TheoryData<object, string, ValueKind> Encodable => new()
    {
        { "123.456.789-09", "123.456.789-09", ValueKind.Text },
        { string.Empty, string.Empty, ValueKind.Text },
        { "abc".ToCharArray(), "abc", ValueKind.Text },
        { 'x', "x", ValueKind.Text },
        { (byte)7, "7", ValueKind.Number },
        { (short)-12, "-12", ValueKind.Number },
        { 1234567890, "1234567890", ValueKind.Number },
        { 12345678909L, "12345678909", ValueKind.Number },
        { ulong.MaxValue, "18446744073709551615", ValueKind.Number },
        { 12345678909m, "12345678909", ValueKind.Number },
        { 12345678909.00m, "12345678909", ValueKind.Number },
    };

    [Theory]
    [MemberData(nameof(Encodable))]
    public void TextoEInteiros_SaoConvertidos(object value, string text, ValueKind kind)
    {
        Assert.True(ValueCodec.TryEncode(value, out string actual, out ValueKind actualKind));
        Assert.Equal(text, actual);
        Assert.Equal(kind, actualKind);
    }

    public static IEnumerable<object[]> NotEncodable =>
    [
        [12.5m],
        [1.0d],
        [1.0f],
        [true],
        [Guid.Empty],
        [new DateTime(2026, 1, 1)],
        [new byte[] { 1, 2 }],
    ];

    [Theory]
    [MemberData(nameof(NotEncodable))]
    public void OutrosValores_NaoSaoConvertidos(object value) =>
        Assert.False(ValueCodec.TryEncode(value, out _, out _));

    public static TheoryData<string, object, object> Decodable => new()
    {
        { "98765432100", "12345678909", "98765432100" },
        { "98765432100", 12345678909L, 98765432100L },
        { "98765432100", 12345678909m, 98765432100m },
        { "98765432100", 12345678909UL, 98765432100UL },
        { "123", 456, 123 },
        { "12", (byte)34, (byte)12 },
        { "y", 'x', 'y' },
    };

    [Theory]
    [MemberData(nameof(Decodable))]
    public void Substituto_VoltaNoTipoOriginal(string substitute, object original, object expected)
    {
        Assert.True(ValueCodec.TryDecode(substitute, original, out object result));
        Assert.Equal(expected, result);
        Assert.Equal(original.GetType(), result.GetType());
    }

    [Fact]
    public void CharArray_VoltaComoCharArray()
    {
        Assert.True(ValueCodec.TryDecode("xyz", "abc".ToCharArray(), out object result));
        Assert.Equal("xyz".ToCharArray(), Assert.IsType<char[]>(result));
    }

    public static TheoryData<string, object> NotDecodable => new()
    {
        { "98765432100", 1234567890 },
        { "4294967296", 1U },
        { "256", (byte)1 },
        { "-1", 1L },
        { "12.5", 1m },
        { "ab", 'x' },
        { "1", 1.0d },
    };

    [Theory]
    [MemberData(nameof(NotDecodable))]
    public void SubstitutoQueNaoCabe_FalhaEDevolveOOriginal(string substitute, object original)
    {
        Assert.False(ValueCodec.TryDecode(substitute, original, out object result));
        Assert.Same(original, result);
    }
}
