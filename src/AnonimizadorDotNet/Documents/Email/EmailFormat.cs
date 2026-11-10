using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Resources;

namespace AnonimizadorDotNet.Documents.Email;

/// <summary>Normalização, validação e montagem de endereços de e-mail.</summary>
/// <remarks>
/// <para>
/// Aceita a forma comum de endereço (<c>parte-local@dominio</c>), não a gramática completa da RFC 5322: parte
/// local de até 64 caracteres entre letras, dígitos e <c>. _ % + -</c>, sem ponto no início, no fim ou
/// repetido; domínio com ao menos dois rótulos de letras, dígitos e hífen, terminando num rótulo só de
/// letras.
/// </para>
/// <para>
/// O domínio de provedores públicos é preservado; o de empresas é substituído, mantendo o sufixo
/// (<c>com.br</c>, <c>gov.br</c>…). A parte local substituta segue o estilo da original: o mesmo separador
/// e a mesma quantidade de dígitos no fim (<c>joao.silva87</c> ⇒ <c>maria.souza12</c>). A etiqueta depois do
/// <c>+</c> não entra na chave nem na saída.
/// </para>
/// </remarks>
internal static class EmailFormat
{
    /// <summary>Tamanho máximo da parte local.</summary>
    public const int MaxLocalPartLength = 64;

    /// <summary>Tamanho máximo do domínio.</summary>
    public const int MaxDomainLength = 253;

    private static readonly SearchValues<char> LocalPartChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789._%+-");

    private static readonly SearchValues<char> DomainChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789.-");

    /// <summary>Normaliza e valida um e-mail.</summary>
    /// <param name="input">O valor como chegou.</param>
    /// <param name="email">O e-mail normalizado.</param>
    /// <returns>Falso se o formato não for o de um e-mail. Nunca lança exceção por causa do conteúdo.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ParsedEmail? email)
    {
        email = null;
        ReadOnlySpan<char> trimmed = input.Trim();
        if (trimmed.IsEmpty || trimmed.Length > MaxLocalPartLength + 1 + MaxDomainLength)
        {
            return false;
        }

        int at = trimmed.IndexOf('@');
        if (at <= 0 || at > MaxLocalPartLength || trimmed[(at + 1)..].Contains('@'))
        {
            return false;
        }

        string local = trimmed[..at].ToString().ToLowerInvariant();
        string domain = trimmed[(at + 1)..].ToString().ToLowerInvariant();
        if (!IsValidLocalPart(local) || !TrySplitDomain(domain, out string? domainName, out string? suffix))
        {
            return false;
        }

        int plus = local.IndexOf('+', StringComparison.Ordinal);
        string key = plus < 0 ? local : local[..plus];
        if (key.Length == 0)
        {
            return false;
        }

        char? separator = null;
        int separatorIndex = key.AsSpan().IndexOfAny('.', '_', '-');
        if (separatorIndex >= 0)
        {
            separator = key[separatorIndex];
        }

        int trailingDigits = key.Length - key.AsSpan().TrimEnd("0123456789").Length;
        bool upper = LetterCasing.Detect(trimmed) == LetterCase.Upper;
        int leading = input.Length - input.TrimStart().Length;
        int trailing = input.Length - input.TrimEnd().Length;

        email = new ParsedEmail(
            key,
            domainName,
            suffix,
            EmbeddedLists.PublicEmailProviders.Contains(domain),
            separator,
            trailingDigits,
            upper,
            input[..leading].ToString(),
            input[^trailing..].ToString());
        return true;
    }

    /// <summary>
    /// Monta uma parte local no estilo da original: prenome e sobrenome sem acentos, em minúsculas, unidos pelo
    /// separador original (ou sem separador) e seguidos dos dígitos.
    /// </summary>
    /// <param name="givenName">Prenome substituto (pode ter acentos e espaços, que são removidos).</param>
    /// <param name="surname">Sobrenome substituto.</param>
    /// <param name="digits">Dígitos a acrescentar; normalmente <see cref="ParsedEmail.TrailingDigits"/> deles.</param>
    /// <param name="original">O e-mail cujo separador é reaplicado.</param>
    /// <exception cref="ArgumentException">O resultado não seria uma parte local válida.</exception>
    public static string ComposeLocalPart(string givenName, string surname, string digits, ParsedEmail original)
    {
        ArgumentNullException.ThrowIfNull(givenName);
        ArgumentNullException.ThrowIfNull(surname);
        ArgumentNullException.ThrowIfNull(digits);
        ArgumentNullException.ThrowIfNull(original);

        var builder = new StringBuilder();
        builder.Append(Slug(givenName));
        if (original.Separator is char separator)
        {
            builder.Append(separator);
        }

        builder.Append(Slug(surname)).Append(digits);
        string local = builder.ToString();
        if (!IsValidLocalPart(local) || digits.AsSpan().ContainsAnyExceptInRange('0', '9'))
        {
            throw new ArgumentException("Os componentes não formam uma parte local de e-mail válida.", nameof(givenName));
        }

        return local;
    }

    /// <summary>Monta o e-mail substituto reaplicando a caixa e os espaços do original.</summary>
    /// <param name="original">O e-mail original.</param>
    /// <param name="localPart">A parte local substituta (por exemplo, de <see cref="ComposeLocalPart"/>).</param>
    /// <param name="domainName">
    /// O nome de domínio substituto, sem sufixo (por exemplo, <c>nortelog</c>). Ignorado para provedores
    /// públicos, cujo domínio é preservado; obrigatório nos demais casos.
    /// </param>
    /// <exception cref="ArgumentException">A parte local ou o domínio não são válidos.</exception>
    public static string Format(ParsedEmail original, string localPart, string? domainName)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(localPart);
        if (!IsValidLocalPart(localPart))
        {
            throw new ArgumentException("A parte local substituta não é válida.", nameof(localPart));
        }

        string domain;
        if (original.IsPublicProvider)
        {
            domain = original.Domain;
        }
        else
        {
            if (string.IsNullOrEmpty(domainName) || !TrySplitDomain(domainName + "." + original.DomainSuffix, out _, out _))
            {
                throw new ArgumentException("O domínio substituto não é válido.", nameof(domainName));
            }

            domain = domainName + "." + original.DomainSuffix;
        }

        string address = localPart + "@" + domain;
        if (original.IsUpperCase)
        {
            address = address.ToUpperInvariant();
        }

        return original.LeadingWhitespace + address + original.TrailingWhitespace;
    }

    private static bool IsValidLocalPart(string local) =>
        local.Length is > 0 and <= MaxLocalPartLength
        && !local.AsSpan().ContainsAnyExcept(LocalPartChars)
        && local[0] != '.'
        && local[^1] != '.'
        && !local.Contains("..", StringComparison.Ordinal);

    private static bool TrySplitDomain(string domain, [NotNullWhen(true)] out string? name, [NotNullWhen(true)] out string? suffix)
    {
        name = null;
        suffix = null;
        if (domain.Length > MaxDomainLength || domain.AsSpan().ContainsAnyExcept(DomainChars))
        {
            return false;
        }

        string[] labels = domain.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (string label in labels)
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-')
            {
                return false;
            }
        }

        string topLevel = labels[^1];
        if (topLevel.Length < 2 || topLevel.AsSpan().ContainsAnyExceptInRange('a', 'z'))
        {
            return false;
        }

        int suffixLabels = labels.Length > 2 && EmbeddedLists.DomainSuffixes.Contains(labels[^2] + "." + labels[^1]) ? 2 : 1;
        name = string.Join('.', labels[..^suffixLabels]);
        suffix = string.Join('.', labels[^suffixLabels..]);
        return true;
    }

    private static string Slug(string text)
    {
        string plain = TextNormalization.RemoveDiacritics(text).ToLowerInvariant();
        var builder = new StringBuilder(plain.Length);
        foreach (char c in plain)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
