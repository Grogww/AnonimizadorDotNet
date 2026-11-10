using AnonimizadorDotNet.Documents.Formatting;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Formatting;

public sealed class NumeralsTests
{
    [Fact]
    public void TryFromChars_Base36_IgnoraACaixa()
    {
        byte[] numerals = new byte[4];

        Assert.True(Numerals.TryFromChars("09aZ", 36, numerals));

        Assert.Equal(new byte[] { 0, 9, 10, 35 }, numerals);
    }

    [Theory]
    [InlineData("12a", 10)]
    [InlineData("12-", 36)]
    [InlineData("é", 36)]
    public void TryFromChars_ForaDoAlfabeto_DevolveFalso(string chars, int radix) =>
        Assert.False(Numerals.TryFromChars(chars, radix, new byte[chars.Length]));

    [Fact]
    public void ToChars_ForaDaBase_Lanca() =>
        Assert.Throws<ArgumentException>(() => Numerals.ToChars([10], 10, new char[1]));

    [Fact]
    public void BaseNaoSuportada_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Numerals.TryFromChars("1", 16, new byte[1]));

    [Theory]
    [InlineData(new byte[] { }, true)]
    [InlineData(new byte[] { 7 }, true)]
    [InlineData(new byte[] { 3, 3, 3 }, true)]
    [InlineData(new byte[] { 3, 3, 4 }, false)]
    public void AllEqual_Funciona(byte[] numerals, bool expected) =>
        Assert.Equal(expected, Numerals.AllEqual(numerals));

    [Fact]
    public void IdaEVolta_Base36_DevolveOsNumerais()
    {
        Gen.Byte[0, 35].Array[1, 20].Sample(numerals =>
        {
            char[] chars = new char[numerals.Length];
            Numerals.ToChars(numerals, 36, chars);
            byte[] back = new byte[numerals.Length];

            Assert.True(Numerals.TryFromChars(chars, 36, back));
            Assert.Equal(numerals, back);
        });
    }
}
