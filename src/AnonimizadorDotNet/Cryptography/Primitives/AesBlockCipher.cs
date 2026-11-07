using System.Security.Cryptography;

namespace AnonimizadorDotNet.Cryptography.Primitives;

/// <summary>
/// AES usado como cifra de bloco pura, com as duas operações de que os algoritmos de FPE precisam:
/// cifrar um único bloco (CIPH) e calcular o CBC-MAC com vetor inicial zero (PRF), conforme
/// NIST SP 800-38G, seção 4.3.
/// </summary>
/// <remarks>
/// <para>
/// Esta classe não é um modo de operação para cifrar mensagens: ela expõe a permutação do AES sobre
/// blocos de 16 bytes. Usá-la para cifrar dados diretamente seria inseguro.
/// </para>
/// <para>Instâncias não são seguras para uso simultâneo por várias threads.</para>
/// </remarks>
internal sealed class AesBlockCipher : IDisposable
{
    /// <summary>Tamanho do bloco do AES, em bytes.</summary>
    public const int BlockSize = 16;

    /// <summary>Limite para usar a pilha nos buffers temporários do CBC-MAC.</summary>
    private const int StackAllocThreshold = 256;

    private static readonly byte[] ZeroIv = new byte[BlockSize];

    private readonly Aes _aes;
    private bool _disposed;

    /// <summary>Cria a cifra a partir de uma chave AES-128, AES-192 ou AES-256.</summary>
    /// <param name="key">Chave de 16, 24 ou 32 bytes. Uma cópia é mantida internamente.</param>
    /// <exception cref="ArgumentException">A chave não tem 16, 24 nem 32 bytes.</exception>
    public AesBlockCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException(
                "A chave AES deve ter 16, 24 ou 32 bytes (AES-128, AES-192 ou AES-256).", nameof(key));
        }

        byte[] keyCopy = key.ToArray();
        try
        {
            _aes = Aes.Create();
            _aes.Key = keyCopy;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyCopy);
        }
    }

    /// <summary>Cifra exatamente um bloco de 16 bytes (função CIPH da norma).</summary>
    /// <param name="block">Bloco de entrada, com 16 bytes.</param>
    /// <param name="destination">Destino, com 16 bytes. Pode ser o mesmo buffer da entrada.</param>
    /// <exception cref="ArgumentException">Entrada ou destino com tamanho diferente de 16 bytes.</exception>
    /// <exception cref="ObjectDisposedException">A instância já foi descartada.</exception>
    public void EncryptBlock(ReadOnlySpan<byte> block, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (block.Length != BlockSize)
        {
            throw new ArgumentException("O bloco de entrada deve ter 16 bytes.", nameof(block));
        }

        if (destination.Length != BlockSize)
        {
            throw new ArgumentException("O destino deve ter 16 bytes.", nameof(destination));
        }

        _aes.EncryptEcb(block, destination, PaddingMode.None);
    }

    /// <summary>
    /// Calcula o CBC-MAC de <paramref name="data"/> com vetor inicial zero (função PRF da norma):
    /// Y₀ = 0¹²⁸, Yⱼ = CIPH(Yⱼ₋₁ ⊕ Xⱼ); o resultado é o último bloco.
    /// </summary>
    /// <param name="data">Dados com tamanho múltiplo de 16 bytes e ao menos um bloco.</param>
    /// <param name="destination">Destino do MAC, com 16 bytes.</param>
    /// <exception cref="ArgumentException">Tamanho dos dados ou do destino inválido.</exception>
    /// <exception cref="ObjectDisposedException">A instância já foi descartada.</exception>
    public void ComputeCbcMac(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data.IsEmpty || data.Length % BlockSize != 0)
        {
            throw new ArgumentException(
                "Os dados do CBC-MAC devem ter tamanho múltiplo de 16 bytes e ao menos um bloco.", nameof(data));
        }

        if (destination.Length != BlockSize)
        {
            throw new ArgumentException("O destino deve ter 16 bytes.", nameof(destination));
        }

        // Os blocos intermediários dependem da entrada; o buffer é zerado antes de ser liberado.
        Span<byte> chain = data.Length <= StackAllocThreshold
            ? stackalloc byte[data.Length]
            : new byte[data.Length];
        try
        {
            _aes.EncryptCbc(data, ZeroIv, chain, PaddingMode.None);
            chain[^BlockSize..].CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chain);
        }
    }

    /// <summary>Descarta a instância do AES e o material de chave que ela mantém.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _aes.Dispose();
        _disposed = true;
    }
}
