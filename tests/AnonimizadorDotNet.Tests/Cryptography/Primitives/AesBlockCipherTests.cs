using AnonimizadorDotNet.Cryptography.Primitives;

namespace AnonimizadorDotNet.Tests.Cryptography.Primitives;

public sealed class AesBlockCipherTests
{
    private static readonly byte[] Aes128Key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");

    [Fact]
    public void EncryptBlock_VetorNistSp80038A_ProduzBlocoEsperado()
    {
        // NIST SP 800-38A, F.1.1 (ECB-AES128), primeiro bloco.
        byte[] plaintext = Convert.FromHexString("6BC1BEE22E409F96E93D7E117393172A");
        byte[] expected = Convert.FromHexString("3AD77BB40D7A3660A89ECAF32466EF97");
        using var cipher = new AesBlockCipher(Aes128Key);
        byte[] actual = new byte[16];

        cipher.EncryptBlock(plaintext, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeCbcMac_RastreioDaAmostra1DoFf1_ProduzREsperado()
    {
        // NIST, exemplos do FF1, amostra #1, rodada 0: R = PRF(P || Q).
        byte[] p = [1, 2, 1, 0, 0, 10, 10, 5, 0, 0, 0, 10, 0, 0, 0, 0];
        byte[] q = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 221, 213];
        byte[] expected = [195, 184, 41, 161, 232, 100, 43, 120, 204, 41, 148, 123, 59, 147, 219, 99];
        using var cipher = new AesBlockCipher(Aes128Key);
        byte[] actual = new byte[16];

        cipher.ComputeCbcMac([.. p, .. q], actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeCbcMac_UmBloco_IgualACifrarOBloco()
    {
        byte[] block = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        using var cipher = new AesBlockCipher(Aes128Key);
        byte[] mac = new byte[16];
        byte[] encrypted = new byte[16];

        cipher.ComputeCbcMac(block, mac);
        cipher.EncryptBlock(block, encrypted);

        Assert.Equal(encrypted, mac);
    }

    [Fact]
    public void ComputeCbcMac_TresBlocos_IgualAEncadearCifrasDeBloco()
    {
        byte[] data = new byte[48];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 7);
        }

        using var cipher = new AesBlockCipher(Aes128Key);
        byte[] expected = new byte[16];
        for (int block = 0; block < 3; block++)
        {
            for (int i = 0; i < 16; i++)
            {
                expected[i] ^= data[(block * 16) + i];
            }

            cipher.EncryptBlock(expected, expected);
        }

        byte[] actual = new byte[16];
        cipher.ComputeCbcMac(data, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeCbcMac_EntradaMaiorQueOLimiteDaPilha_ProduzMesmoResultadoDoEncadeamento()
    {
        byte[] data = new byte[512];
        Random.Shared.NextBytes(data);
        using var cipher = new AesBlockCipher(Aes128Key);
        byte[] expected = new byte[16];
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            for (int i = 0; i < 16; i++)
            {
                expected[i] ^= data[offset + i];
            }

            cipher.EncryptBlock(expected, expected);
        }

        byte[] actual = new byte[16];
        cipher.ComputeCbcMac(data, actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void Construtor_ChaveComTamanhoInvalido_LancaArgumentException(int keyLength)
    {
        byte[] key = new byte[keyLength];

        var exception = Assert.Throws<ArgumentException>(() => new AesBlockCipher(key));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void Construtor_ChaveComTamanhoValido_NaoLanca(int keyLength)
    {
        using var cipher = new AesBlockCipher(new byte[keyLength]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void EncryptBlock_BlocoComTamanhoInvalido_LancaArgumentException(int length)
    {
        using var cipher = new AesBlockCipher(Aes128Key);

        Assert.Throws<ArgumentException>(() => cipher.EncryptBlock(new byte[length], new byte[16]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(33)]
    public void ComputeCbcMac_DadosComTamanhoInvalido_LancaArgumentException(int length)
    {
        using var cipher = new AesBlockCipher(Aes128Key);

        Assert.Throws<ArgumentException>(() => cipher.ComputeCbcMac(new byte[length], new byte[16]));
    }

    [Fact]
    public void Operacoes_AposDispose_LancamObjectDisposedException()
    {
        var cipher = new AesBlockCipher(Aes128Key);
        cipher.Dispose();
        cipher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cipher.EncryptBlock(new byte[16], new byte[16]));
        Assert.Throws<ObjectDisposedException>(() => cipher.ComputeCbcMac(new byte[16], new byte[16]));
    }
}
