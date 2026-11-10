namespace AnonimizadorDotNet.Documents.Email;

/// <summary>Um e-mail já normalizado e separado em parte local e domínio.</summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class ParsedEmail
{
    internal ParsedEmail(
        string localPart,
        string domainName,
        string domainSuffix,
        bool isPublicProvider,
        char? separator,
        int trailingDigits,
        bool isUpperCase,
        string leadingWhitespace,
        string trailingWhitespace)
    {
        LocalPart = localPart;
        DomainName = domainName;
        DomainSuffix = domainSuffix;
        IsPublicProvider = isPublicProvider;
        Separator = separator;
        TrailingDigits = trailingDigits;
        IsUpperCase = isUpperCase;
        LeadingWhitespace = leadingWhitespace;
        TrailingWhitespace = trailingWhitespace;
    }

    /// <summary>
    /// A parte local em minúsculas e sem a etiqueta depois do <c>+</c>: <c>Joao.Silva+news</c> vira
    /// <c>joao.silva</c>. É a entrada da seleção por HMAC.
    /// </summary>
    public string LocalPart { get; }

    /// <summary>
    /// O domínio sem o sufixo, em minúsculas (<c>empresa</c> em <c>empresa.com.br</c>, <c>mail.empresa</c> em
    /// <c>mail.empresa.com.br</c>).
    /// </summary>
    public string DomainName { get; }

    /// <summary>O sufixo do domínio, preservado (<c>com.br</c>, <c>gov.br</c>, <c>com</c>…).</summary>
    public string DomainSuffix { get; }

    /// <summary>O domínio completo, em minúsculas.</summary>
    public string Domain => DomainName + "." + DomainSuffix;

    /// <summary>Verdadeiro para provedores públicos (gmail.com, hotmail.com…): o domínio é preservado.</summary>
    public bool IsPublicProvider { get; }

    /// <summary>O primeiro separador da parte local (<c>.</c>, <c>_</c> ou <c>-</c>), ou nulo se não houver.</summary>
    public char? Separator { get; }

    /// <summary>Quantidade de dígitos no fim da parte local (<c>joao.silva87</c> tem 2).</summary>
    public int TrailingDigits { get; }

    /// <summary>Verdadeiro quando todas as letras estavam em maiúsculas; senão a saída sai em minúsculas.</summary>
    public bool IsUpperCase { get; }

    /// <summary>Espaços antes do e-mail (comuns em colunas de tamanho fixo), reaplicados.</summary>
    public string LeadingWhitespace { get; }

    /// <summary>Espaços depois do e-mail, reaplicados.</summary>
    public string TrailingWhitespace { get; }
}
