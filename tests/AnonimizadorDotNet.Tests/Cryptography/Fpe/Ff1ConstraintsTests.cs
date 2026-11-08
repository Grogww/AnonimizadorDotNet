using System.Numerics;
using AnonimizadorDotNet.Cryptography.Fpe;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class Ff1ConstraintsTests
{
    [Theory]
    [InlineData(2, 20)]
    [InlineData(3, 13)]
    [InlineData(10, 6)]
    [InlineData(16, 5)]
    [InlineData(26, 5)]
    [InlineData(36, 4)]
    public void GetMinLength_Radix_DevolveMenorTamanhoComDominioDeUmMilhao(int radix, int expected)
    {
        Assert.Equal(expected, Ff1Constraints.GetMinLength(radix));
    }

    [Fact]
    public void GetMinLength_TodoRadixValido_EOMenorTamanhoQueAtingeODominioMinimo()
    {
        for (int radix = Ff1Constraints.MinRadix; radix <= Ff1Constraints.MaxRadix; radix++)
        {
            int minLength = Ff1Constraints.GetMinLength(radix);

            Assert.True(minLength >= 2);
            Assert.True(BigInteger.Pow(radix, minLength) >= Ff1Constraints.MinDomainSize);
            Assert.True(minLength == 2 || BigInteger.Pow(radix, minLength - 1) < Ff1Constraints.MinDomainSize);
        }
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(65536)]
    public void ValidateRadix_ForaDoIntervalo_LancaArgumentOutOfRangeException(int radix)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Ff1Constraints.ValidateRadix(radix, nameof(radix)));

        Assert.Equal("radix", exception.ParamName);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(Ff1Constraints.MaxLength + 1)]
    public void ValidateLength_ForaDoIntervalo_LancaArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => Ff1Constraints.ValidateLength(length, 6, "input"));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(Ff1Constraints.MaxLength)]
    public void ValidateLength_NoLimite_NaoLanca(int length)
    {
        Ff1Constraints.ValidateLength(length, 6, "input");
    }

    [Fact]
    public void ValidateTweak_AcimaDoLimite_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Ff1Constraints.ValidateTweak(Ff1Constraints.MaxTweakLength + 1, "tweak"));
    }
}
