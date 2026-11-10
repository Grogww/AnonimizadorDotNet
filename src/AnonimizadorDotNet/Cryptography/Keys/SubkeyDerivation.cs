using System.Security.Cryptography;

namespace AnonimizadorDotNet.Cryptography.Keys;

/// <summary>
/// Deriva da chave mestra uma subchave por finalidade e tipo de documento, com HKDF-SHA256 (RFC 5869).
/// </summary>
/// <remarks>
/// <para>
/// Mesma chave mestra + mesmo rótulo ⇒ mesma subchave; rótulos diferentes ⇒ subchaves independentes.
/// Conhecer uma subchave não revela a chave mestra nem as demais subchaves.
/// </para>
/// <para>
/// O salt é vazio (equivale a 32 bytes zero, RFC 5869, seção 2.2): a chave mestra já deve ser aleatória,
/// e a separação entre usos fica toda no rótulo. Uma senha digitada não serve como chave mestra.
/// </para>
/// <para>
/// O chamador é dono do destino e deve zerá-lo com
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> quando a subchave não for mais usada.
/// </para>
/// </remarks>
internal static class SubkeyDerivation
{
    /// <summary>Menor tamanho aceito para a chave mestra, em bytes (256 bits).</summary>
    public const int MinMasterKeySize = 32;

    /// <summary>Tamanho de toda subchave derivada, em bytes: chave AES-256 ou chave HMAC-SHA256.</summary>
    public const int SubkeySize = 32;

    /// <summary>Deriva a subchave de um rótulo.</summary>
    /// <param name="masterKey">Chave mestra, com ao menos <see cref="MinMasterKeySize"/> bytes.</param>
    /// <param name="label">Rótulo de derivação, normalmente um de <see cref="KeyLabels"/>.</param>
    /// <param name="destination">Destino da subchave, com exatamente <see cref="SubkeySize"/> bytes.</param>
    /// <exception cref="ArgumentException">Chave mestra curta demais, rótulo vazio ou destino com tamanho errado.</exception>
    public static void Derive(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> label, Span<byte> destination)
    {
        if (masterKey.Length < MinMasterKeySize)
        {
            throw new ArgumentException(
                $"A chave mestra deve ter ao menos {MinMasterKeySize} bytes (256 bits).", nameof(masterKey));
        }

        if (label.IsEmpty)
        {
            throw new ArgumentException("O rótulo de derivação não pode ser vazio.", nameof(label));
        }

        if (destination.Length != SubkeySize)
        {
            throw new ArgumentException($"O destino deve ter {SubkeySize} bytes.", nameof(destination));
        }

        HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, destination, salt: [], info: label);
    }
}
