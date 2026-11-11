using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AnonimizadorDotNet.Cryptography.Fpe;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Cpf;

namespace AnonimizadorDotNet.Transformation.Cpf;

/// <summary>Substitui um CPF por outro CPF válido, cifrando a base de 9 dígitos com FF1.</summary>
/// <remarks>
/// <para>
/// A base é cifrada com <i>cycle-walking</i> para nunca cair em dígitos todos iguais, e os verificadores são
/// recalculados. O tweak é vazio. A representação da entrada (máscara, texto ou número, zeros à esquerda) é
/// reaplicada na saída.
/// </para>
/// <para>Seguro para uso por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class CpfTransformer : IValueTransformer, IDisposable
{
    private readonly Ff1Pool _pool;

    /// <inheritdoc/>
    public string Type => DocumentTypeNames.Cpf;

    /// <summary>Cria o transformador com a subchave de CPF do chaveiro.</summary>
    public CpfTransformer(KeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Span<byte> subkey = stackalloc byte[SubkeyDerivation.SubkeySize];
        try
        {
            keys.DeriveSubkey(KeyLabels.Ff1Cpf, subkey);
            _pool = new Ff1Pool(subkey, 10);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>Gera o CPF substituto.</summary>
    /// <param name="value">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="substitute">O CPF substituto, na representação da entrada.</param>
    /// <returns>Falso se o valor não for um CPF válido. Nunca lança exceção por causa do conteúdo.</returns>
    /// <exception cref="ObjectDisposedException">O transformador já foi descartado.</exception>
    public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute) =>
        TryApply(value, kind, encrypt: true, out substitute);

    /// <summary>Desfaz <see cref="TryTransform"/> (usado para verificar a bijeção).</summary>
    internal bool TryReverse(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? original) =>
        TryApply(value, kind, encrypt: false, out original);

    /// <summary>Descarta as instâncias do FF1 e zera a subchave.</summary>
    public void Dispose() => _pool.Dispose();

    private bool TryApply(ReadOnlySpan<char> value, ValueKind kind, bool encrypt, [NotNullWhen(true)] out string? result)
    {
        result = null;
        if (!CpfFormat.TryParse(value, kind, out ParsedCpf? cpf))
        {
            return false;
        }

        Span<byte> output = stackalloc byte[CpfFormat.BaseLength];
        if (encrypt)
        {
            _pool.Encrypt(cpf.Base, [], output, CpfFormat.IsAllowedBase);
        }
        else
        {
            _pool.Decrypt(cpf.Base, [], output, CpfFormat.IsAllowedBase);
        }

        result = CpfFormat.Format(output, cpf.Layout);
        return true;
    }
}
