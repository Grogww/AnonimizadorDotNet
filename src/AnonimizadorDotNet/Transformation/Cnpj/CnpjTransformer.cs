using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AnonimizadorDotNet.Cryptography.Fpe;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Transformation.Cnpj;

/// <summary>Substitui um CNPJ por outro CNPJ válido, cifrando só a raiz com FF1.</summary>
/// <remarks>
/// <para>
/// A raiz numérica (base 10) e a alfanumérica (base 36) usam subchaves e permutações próprias, cada uma com
/// <i>cycle-walking</i> no seu subconjunto permitido: CNPJ numérico gera numérico (sem dígitos todos iguais)
/// e alfanumérico gera alfanumérico (com ao menos uma letra). A ordem é preservada e os verificadores são
/// recalculados. O tweak é vazio, para que matriz e filiais (mesma raiz, ordens diferentes) continuem com a
/// mesma raiz substituta.
/// </para>
/// <para>Seguro para uso por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class CnpjTransformer : IDisposable
{
    private readonly Ff1Pool _numeric;
    private readonly Ff1Pool _alphanumeric;

    /// <summary>Cria o transformador com as subchaves de CNPJ do chaveiro.</summary>
    public CnpjTransformer(KeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Span<byte> subkey = stackalloc byte[SubkeyDerivation.SubkeySize];
        try
        {
            keys.DeriveSubkey(KeyLabels.Ff1Cnpj, subkey);
            _numeric = new Ff1Pool(subkey, 10);
            keys.DeriveSubkey(KeyLabels.Ff1CnpjAlphanumeric, subkey);
            _alphanumeric = new Ff1Pool(subkey, 36);
        }
        catch
        {
            _numeric?.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>Gera o CNPJ substituto.</summary>
    /// <param name="value">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="substitute">O CNPJ substituto, na representação e na caixa da entrada.</param>
    /// <returns>Falso se o valor não for um CNPJ válido. Nunca lança exceção por causa do conteúdo.</returns>
    /// <exception cref="ObjectDisposedException">O transformador já foi descartado.</exception>
    public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute) =>
        TryApply(value, kind, encrypt: true, out substitute);

    /// <summary>Desfaz <see cref="TryTransform"/> (usado para verificar a bijeção).</summary>
    internal bool TryReverse(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? original) =>
        TryApply(value, kind, encrypt: false, out original);

    /// <summary>Descarta as instâncias do FF1 e zera as subchaves.</summary>
    public void Dispose()
    {
        _numeric.Dispose();
        _alphanumeric.Dispose();
    }

    private bool TryApply(ReadOnlySpan<char> value, ValueKind kind, bool encrypt, [NotNullWhen(true)] out string? result)
    {
        result = null;
        if (!CnpjFormat.TryParse(value, kind, out ParsedCnpj? cnpj))
        {
            return false;
        }

        (Ff1Pool pool, NumeralPredicate isAllowed) = cnpj.IsAlphanumericRoot
            ? (_alphanumeric, CnpjFormat.IsAllowedAlphanumericRoot)
            : (_numeric, CnpjFormat.IsAllowedNumericRoot);

        Span<byte> root = stackalloc byte[CnpjFormat.RootLength];
        if (encrypt)
        {
            pool.Encrypt(cnpj.Root, [], root, isAllowed);
        }
        else
        {
            pool.Decrypt(cnpj.Root, [], root, isAllowed);
        }

        result = CnpjFormat.Format(root, cnpj);
        return true;
    }
}
