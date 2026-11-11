using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AnonimizadorDotNet.Cryptography.Fpe;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Phone;

namespace AnonimizadorDotNet.Transformation.Phone;

/// <summary>Substitui um telefone por outro, cifrando os 7 dígitos finais com FF1.</summary>
/// <remarks>
/// <para>
/// Código do país, DDD e o início do assinante (o primeiro dígito e, no celular, o 9) são preservados e
/// entram no tweak (<see cref="PhoneTweak"/>). Como o primeiro dígito não muda, o substituto fica sempre na
/// mesma faixa (fixo ou celular) sem <i>cycle-walking</i>.
/// </para>
/// <para>Seguro para uso por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class PhoneTransformer : IValueTransformer, IDisposable
{
    private readonly Ff1Pool _pool;

    /// <inheritdoc/>
    public string Type => DocumentTypeNames.Phone;

    /// <summary>Cria o transformador com a subchave de telefone do chaveiro.</summary>
    public PhoneTransformer(KeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Span<byte> subkey = stackalloc byte[SubkeyDerivation.SubkeySize];
        try
        {
            keys.DeriveSubkey(KeyLabels.Ff1Phone, subkey);
            _pool = new Ff1Pool(subkey, 10);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>Gera o telefone substituto.</summary>
    /// <param name="value">O valor como chegou.</param>
    /// <param name="kind">Se chegou como texto ou como número.</param>
    /// <param name="substitute">O telefone substituto, na representação da entrada.</param>
    /// <returns>Falso se o valor não for um telefone válido. Nunca lança exceção por causa do conteúdo.</returns>
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
        if (!PhoneFormat.TryParse(value, kind, out ParsedPhone? phone))
        {
            return false;
        }

        Span<byte> tweak = stackalloc byte[PhoneTweak.MaxLength];
        tweak = tweak[..PhoneTweak.Write(phone, tweak)];

        Span<byte> subscriber = stackalloc byte[PhoneFormat.SubscriberLength];
        if (encrypt)
        {
            _pool.Encrypt(phone.Subscriber, tweak, subscriber);
        }
        else
        {
            _pool.Decrypt(phone.Subscriber, tweak, subscriber);
        }

        result = PhoneFormat.Format(subscriber, phone);
        return true;
    }
}
