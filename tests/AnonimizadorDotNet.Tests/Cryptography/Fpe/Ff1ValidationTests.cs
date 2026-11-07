using AnonimizadorDotNet.Cryptography.Fpe;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class Ff1ValidationTests
{
    private static readonly byte[] Key = new byte[16];

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(33)]
    public void Construtor_ChaveComTamanhoInvalido_LancaArgumentException(int keyLength)
    {
        Assert.Throws<ArgumentException>(() => new Ff1(new byte[keyLength], 10));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    public void Construtor_RadixForaDoIntervalo_LancaArgumentOutOfRangeException(int radix)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ff1(Key, radix));
    }

    [Theory]
    [InlineData(10, 6)]
    [InlineData(36, 4)]
    public void Construtor_Radix_ExpoeTamanhoMinimo(int radix, int expectedMinLength)
    {
        using var ff1 = new Ff1(Key, radix);

        Assert.Equal(radix, ff1.Radix);
        Assert.Equal(expectedMinLength, ff1.MinLength);
    }

    [Fact]
    public void Encrypt_EntradaAbaixoDoMinimo_LancaArgumentException()
    {
        using var ff1 = new Ff1(Key, 10);

        var exception = Assert.Throws<ArgumentException>(() => ff1.Encrypt(new byte[5], [], new byte[5]));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void Encrypt_EntradaAcimaDoMaximo_LancaArgumentException()
    {
        using var ff1 = new Ff1(Key, 10);
        int length = Ff1Constraints.MaxLength + 1;

        Assert.Throws<ArgumentException>(() => ff1.Encrypt(new byte[length], [], new byte[length]));
    }

    [Fact]
    public void Encrypt_EntradaNoMaximo_Funciona()
    {
        using var ff1 = new Ff1(Key, 36);
        byte[] input = new byte[Ff1Constraints.MaxLength];
        byte[] ciphertext = new byte[input.Length];
        byte[] plaintext = new byte[input.Length];

        ff1.Encrypt(input, new byte[Ff1Constraints.MaxTweakLength], ciphertext);
        ff1.Decrypt(ciphertext, new byte[Ff1Constraints.MaxTweakLength], plaintext);

        Assert.Equal(input, plaintext);
    }

    [Fact]
    public void Encrypt_TweakAcimaDoMaximo_LancaArgumentException()
    {
        using var ff1 = new Ff1(Key, 10);

        var exception = Assert.Throws<ArgumentException>(
            () => ff1.Encrypt(new byte[9], new byte[Ff1Constraints.MaxTweakLength + 1], new byte[9]));

        Assert.Equal("tweak", exception.ParamName);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void Encrypt_DestinoComTamanhoDiferente_LancaArgumentException(int outputLength)
    {
        using var ff1 = new Ff1(Key, 10);

        var exception = Assert.Throws<ArgumentException>(() => ff1.Encrypt(new byte[9], [], new byte[outputLength]));

        Assert.Equal("output", exception.ParamName);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 255)]
    [InlineData(36, 36)]
    public void Encrypt_NumeralForaDoAlfabeto_LancaArgumentException(int radix, byte invalidNumeral)
    {
        using var ff1 = new Ff1(Key, radix);
        byte[] input = [1, 2, 3, 4, 5, 6, 7, 8, invalidNumeral];

        var exception = Assert.Throws<ArgumentException>(() => ff1.Encrypt(input, [], new byte[input.Length]));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void Encrypt_EntradaInvalida_MensagemNaoExpoeAEntrada()
    {
        // Simula um CPF com um numeral corrompido: a mensagem não pode conter os dígitos.
        using var ff1 = new Ff1(Key, 10);
        byte[] input = [5, 2, 9, 9, 8, 2, 2, 4, 7, 2, 5];
        input[^1] = 10;

        var exception = Assert.Throws<ArgumentException>(() => ff1.Encrypt(input, [], new byte[input.Length]));

        Assert.DoesNotContain("5299822472", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Operacoes_AposDispose_LancamObjectDisposedException()
    {
        var ff1 = new Ff1(Key, 10);
        ff1.Dispose();
        ff1.Dispose();

        Assert.Throws<ObjectDisposedException>(() => ff1.Encrypt(new byte[9], [], new byte[9]));
        Assert.Throws<ObjectDisposedException>(() => ff1.Decrypt(new byte[9], [], new byte[9]));
    }
}
