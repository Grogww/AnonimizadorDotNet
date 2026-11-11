using System.Security.Cryptography;

namespace AnonimizadorDotNet.Cryptography.Keys;

/// <summary>Material de chave guardado por toda a vida de um componente, zerado no descarte.</summary>
/// <remarks>
/// <para>
/// Os bytes ficam num array fixado na memória (<i>pinned</i>), para que o coletor de lixo não deixe cópias
/// espalhadas ao compactar o heap. <see cref="Dispose"/> zera o conteúdo com
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>; depois disso, ler a chave lança exceção.
/// </para>
/// <para>A leitura pode ser feita por várias threads ao mesmo tempo; descartar durante o uso não.</para>
/// </remarks>
internal sealed class SecretKey : IDisposable
{
    private readonly byte[] _bytes;
    private bool _disposed;

    /// <summary>Copia o material de chave; o buffer do chamador não fica retido.</summary>
    /// <param name="key">Material de chave; não pode ser vazio.</param>
    /// <exception cref="ArgumentException">Chave vazia.</exception>
    public SecretKey(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("A chave não pode ser vazia.", nameof(key));
        }

        _bytes = GC.AllocateUninitializedArray<byte>(key.Length, pinned: true);
        key.CopyTo(_bytes);
    }

    /// <summary>Tamanho da chave, em bytes.</summary>
    public int Length => _bytes.Length;

    /// <summary>Os bytes da chave.</summary>
    /// <exception cref="ObjectDisposedException">A chave já foi descartada.</exception>
    public ReadOnlySpan<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes;
        }
    }

    /// <summary>Zera a chave.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
    }
}
