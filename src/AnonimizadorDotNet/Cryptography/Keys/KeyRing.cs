using System.Security.Cryptography;

namespace AnonimizadorDotNet.Cryptography.Keys;

/// <summary>Guarda a chave mestra e deriva dela as subchaves de cada finalidade e tipo de documento.</summary>
/// <remarks>
/// <para>
/// A chave mestra é copiada para uma <see cref="SecretKey"/>: o buffer do chamador não fica retido e pode
/// ser zerado logo depois da construção. <see cref="Dispose"/> zera a cópia.
/// </para>
/// <para>
/// As subchaves saem de <see cref="SubkeyDerivation"/>. Quem pede uma subchave é dono dela: um destino
/// temporário deve ser zerado depois do uso; uma <see cref="SecretKey"/> deve ser descartada.
/// </para>
/// <para>A derivação pode ser feita por várias threads ao mesmo tempo; descartar durante o uso não.</para>
/// </remarks>
internal sealed class KeyRing : IDisposable
{
    private readonly SecretKey _masterKey;

    /// <summary>Copia a chave mestra.</summary>
    /// <param name="masterKey">Chave mestra aleatória, com ao menos <see cref="SubkeyDerivation.MinMasterKeySize"/> bytes.</param>
    /// <exception cref="ArgumentException">Chave mestra curta demais.</exception>
    public KeyRing(ReadOnlySpan<byte> masterKey)
    {
        if (masterKey.Length < SubkeyDerivation.MinMasterKeySize)
        {
            throw new ArgumentException(
                $"A chave mestra deve ter ao menos {SubkeyDerivation.MinMasterKeySize} bytes (256 bits).",
                nameof(masterKey));
        }

        _masterKey = new SecretKey(masterKey);
    }

    /// <summary>Deriva a subchave de um rótulo num destino do chamador.</summary>
    /// <param name="label">Rótulo de derivação, normalmente um de <see cref="KeyLabels"/>.</param>
    /// <param name="destination">Destino, com exatamente <see cref="SubkeyDerivation.SubkeySize"/> bytes.</param>
    /// <exception cref="ArgumentException">Rótulo vazio ou destino com tamanho errado.</exception>
    /// <exception cref="ObjectDisposedException">O chaveiro já foi descartado.</exception>
    public void DeriveSubkey(ReadOnlySpan<byte> label, Span<byte> destination) =>
        SubkeyDerivation.Derive(_masterKey.Span, label, destination);

    /// <summary>Deriva a subchave de um rótulo e a guarda numa <see cref="SecretKey"/>.</summary>
    /// <param name="label">Rótulo de derivação, normalmente um de <see cref="KeyLabels"/>.</param>
    /// <exception cref="ArgumentException">Rótulo vazio.</exception>
    /// <exception cref="ObjectDisposedException">O chaveiro já foi descartado.</exception>
    public SecretKey CreateSubkey(ReadOnlySpan<byte> label)
    {
        Span<byte> subkey = stackalloc byte[SubkeyDerivation.SubkeySize];
        try
        {
            DeriveSubkey(label, subkey);
            return new SecretKey(subkey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>Zera a cópia da chave mestra.</summary>
    public void Dispose() => _masterKey.Dispose();
}
