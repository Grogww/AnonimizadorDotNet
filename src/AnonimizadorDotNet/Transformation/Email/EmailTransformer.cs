using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Cryptography.Selection;
using AnonimizadorDotNet.Documents.Email;
using AnonimizadorDotNet.Resources;

namespace AnonimizadorDotNet.Transformation.Email;

/// <summary>Substitui um e-mail por um endereço fictício escolhido nos dicionários por HMAC.</summary>
/// <remarks>
/// <para>
/// A parte local vira prenome + sobrenome + dígitos, com o separador e a quantidade de dígitos do original
/// (<see cref="EmailFormat.ComposeLocalPart"/>). A chave da seleção é a parte local inteira, normalizada e
/// sem a etiqueta depois do <c>+</c>.
/// </para>
/// <para>
/// O domínio de provedor público é preservado. O de empresa vira começo + final de domínio fictício, com o
/// mesmo sufixo, e a chave da seleção é o domínio inteiro: todos os endereços de uma empresa ficam com o
/// mesmo domínio substituto. Colisões são possíveis e aceitas: o e-mail não relaciona registros.
/// </para>
/// <para>Seguro para uso por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class EmailTransformer : IDisposable
{
    private readonly SecretKey _key;

    /// <summary>Cria o transformador com a subchave de e-mail do chaveiro.</summary>
    public EmailTransformer(KeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _key = keys.CreateSubkey(KeyLabels.HmacEmail);
    }

    /// <summary>Gera o e-mail substituto.</summary>
    /// <param name="value">O e-mail como chegou.</param>
    /// <param name="substitute">O e-mail substituto, no estilo do original.</param>
    /// <returns>Falso se o valor não for um e-mail. Nunca lança exceção por causa do conteúdo.</returns>
    /// <exception cref="ObjectDisposedException">O transformador já foi descartado.</exception>
    public bool TryTransform(ReadOnlySpan<char> value, [NotNullWhen(true)] out string? substitute)
    {
        substitute = null;
        ReadOnlySpan<byte> key = _key.Span;
        if (!EmailFormat.TryParse(value, out ParsedEmail? email))
        {
            return false;
        }

        byte[] local = Encoding.UTF8.GetBytes(email.LocalPart);
        ImmutableArray<string> givenNames = HmacSelector.SelectIndex(key, HmacContexts.EmailGender, local, 2) == 0
            ? EmbeddedLists.MaleGivenNames
            : EmbeddedLists.FemaleGivenNames;
        string givenName = givenNames[HmacSelector.SelectIndex(key, HmacContexts.EmailGivenName, local, givenNames.Length)];
        string surname = EmbeddedLists.Surnames[HmacSelector.SelectIndex(key, HmacContexts.EmailSurname, local, EmbeddedLists.Surnames.Length)];

        // Os nomes encurtam ao virar parte local (sem acentos e espaços), então este limite é conservador.
        int room = EmailFormat.MaxLocalPartLength - givenName.Length - surname.Length - 1;
        int digitCount = Math.Clamp(email.TrailingDigits, 0, Math.Max(room, 0));
        var digits = new StringBuilder(digitCount);
        for (int position = 0; position < digitCount; position++)
        {
            digits.Append((char)('0' + HmacSelector.SelectIndex(key, HmacContexts.EmailDigit(position), local, 10)));
        }

        string localPart = EmailFormat.ComposeLocalPart(givenName, surname, digits.ToString(), email);
        string? domainName = null;
        if (!email.IsPublicProvider)
        {
            byte[] domain = Encoding.UTF8.GetBytes(email.Domain);
            ImmutableArray<string> starts = EmbeddedLists.DomainStarts;
            ImmutableArray<string> endings = EmbeddedLists.DomainEndings;
            domainName = starts[HmacSelector.SelectIndex(key, HmacContexts.EmailDomainStart, domain, starts.Length)]
                + endings[HmacSelector.SelectIndex(key, HmacContexts.EmailDomainEnding, domain, endings.Length)];
        }

        substitute = EmailFormat.Format(email, localPart, domainName);
        return true;
    }

    /// <summary>Zera a subchave.</summary>
    public void Dispose() => _key.Dispose();
}
