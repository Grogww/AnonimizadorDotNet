using System.Numerics;
using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class ByteStringTests
{
    [Fact]
    public void ToInteger_BigEndianSemSinal_DevolveValorEsperado()
    {
        Assert.Equal(new BigInteger(0xFF01), ByteString.ToInteger([0xFF, 0x01]));
        Assert.Equal(new BigInteger(0x80), ByteString.ToInteger([0x00, 0x80]));
    }

    [Fact]
    public void ToInteger_CadeiaVazia_DevolveZero()
    {
        Assert.Equal(BigInteger.Zero, ByteString.ToInteger([]));
    }

    [Fact]
    public void FromInteger_ValorDaAmostra1_EscreveComZerosAEsquerda()
    {
        // NIST, exemplos do FF1, amostra #1, rodada 0: [NUM₁₀(56789)]³ = [0, 221, 213].
        byte[] actual = [9, 9, 9];

        ByteString.FromInteger(56789, actual);

        Assert.Equal([0, 221, 213], actual);
    }

    [Fact]
    public void FromInteger_Zero_PreencheComZeros()
    {
        byte[] actual = [1, 2, 3, 4];

        ByteString.FromInteger(BigInteger.Zero, actual);

        Assert.Equal([0, 0, 0, 0], actual);
    }

    [Fact]
    public void FromInteger_ValorComBitAltoLigado_NaoAcrescentaByteDeSinal()
    {
        byte[] actual = new byte[1];

        ByteString.FromInteger(255, actual);

        Assert.Equal([255], actual);
    }

    [Fact]
    public void FromInteger_ValorQueNaoCabe_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteString.FromInteger(256, new byte[1]));
    }

    [Fact]
    public void FromInteger_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteString.FromInteger(-1, new byte[4]));
    }

    [Fact]
    public void IdaEVolta_QualquerCadeia_PreservaBytes()
    {
        Gen.Byte.Array[0, 64].Sample(bytes =>
        {
            byte[] roundTrip = new byte[bytes.Length];
            ByteString.FromInteger(ByteString.ToInteger(bytes), roundTrip);
            Assert.Equal(bytes, roundTrip);
        });
    }
}
