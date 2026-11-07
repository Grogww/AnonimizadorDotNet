using System.Numerics;
using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class NumeralStringTests
{
    [Theory]
    [InlineData(10, 11010)]
    [InlineData(2, 26)]
    public void ToInteger_ExemploDaNorma_DevolveValorEsperado(int radix, int expected)
    {
        // NIST SP 800-38G, seção 4.5: NUM₁₀(00011010) = 11010 e NUM₂(00011010) = 26.
        byte[] numerals = [0, 0, 0, 1, 1, 0, 1, 0];

        Assert.Equal(new BigInteger(expected), NumeralString.ToInteger(numerals, radix));
    }

    [Fact]
    public void FromInteger_ExemploDaNorma_DevolveNumeraisEsperados()
    {
        // NIST SP 800-38G, seção 4.5: STR⁴₁₂(559) = [0, 3, 10, 7].
        byte[] actual = new byte[4];

        NumeralString.FromInteger(559, 12, actual);

        Assert.Equal([0, 3, 10, 7], actual);
    }

    [Fact]
    public void ToInteger_CadeiaVazia_DevolveZero()
    {
        Assert.Equal(BigInteger.Zero, NumeralString.ToInteger([], 10));
    }

    [Fact]
    public void FromInteger_Zero_PreencheComZeros()
    {
        byte[] actual = [9, 9, 9];

        NumeralString.FromInteger(0, 10, actual);

        Assert.Equal([0, 0, 0], actual);
    }

    [Fact]
    public void FromInteger_ValorQueNaoCabe_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeralString.FromInteger(1000, 10, new byte[3]));
    }

    [Fact]
    public void FromInteger_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeralString.FromInteger(-1, 10, new byte[3]));
    }

    [Fact]
    public void IdaEVolta_QualquerCadeia_PreservaNumerais()
    {
        Gen<(int Radix, byte[] Numerals)> cases =
            from radix in Gen.Int[Ff1Constraints.MinRadix, Ff1Constraints.MaxRadix]
            from numerals in Gen.Byte[0, (byte)(radix - 1)].Array[0, 64]
            select (radix, numerals);

        cases.Sample(c =>
        {
            byte[] roundTrip = new byte[c.Numerals.Length];
            NumeralString.FromInteger(NumeralString.ToInteger(c.Numerals, c.Radix), c.Radix, roundTrip);
            Assert.Equal(c.Numerals, roundTrip);
        });
    }
}
