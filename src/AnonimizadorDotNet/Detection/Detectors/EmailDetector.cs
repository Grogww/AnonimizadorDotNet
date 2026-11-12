using System.Buffers;
using AnonimizadorDotNet.Documents.Email;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Detection.Detectors;

/// <summary>Encontra endereços de e-mail na forma aceita pelo <see cref="EmailFormat"/>.</summary>
/// <remarks>
/// <para>
/// Parte de cada <c>@</c> e se expande para os dois lados pelos caracteres permitidos, em vez de usar uma
/// regex: um padrão que começa pela parte local tentaria cada posição de um texto longo sem <c>@</c>, com
/// custo quadrático. Assim cada caractere é visto no máximo duas vezes.
/// </para>
/// <para>
/// Pontos no começo da parte local e pontos ou hífens no fim do domínio ficam fora do trecho
/// (<c>escreva para joao@empresa.com.br.</c>). O trecho só é aceito se o <see cref="EmailFormat"/> o validar.
/// </para>
/// </remarks>
internal sealed class EmailDetector : PatternDetector
{
    private static readonly SearchValues<char> LocalChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._%+-");

    private static readonly SearchValues<char> DomainChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-");

    private EmailDetector()
    {
    }

    /// <summary>A instância única (o detector não tem estado).</summary>
    public static EmailDetector Instance { get; } = new();

    /// <inheritdoc/>
    public override string Type => DocumentTypeNames.Email;

    /// <inheritdoc/>
    public override void Collect(string text, List<TextMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(matches);

        int at = text.IndexOf('@', StringComparison.Ordinal);
        while (at >= 0)
        {
            int start = at;
            while (start > 0 && LocalChars.Contains(text[start - 1]))
            {
                start--;
            }

            while (start < at && text[start] == '.')
            {
                start++;
            }

            int end = at + 1;
            while (end < text.Length && DomainChars.Contains(text[end]))
            {
                end++;
            }

            while (end > at + 1 && text[end - 1] is '.' or '-')
            {
                end--;
            }

            if (start < at && end > at + 1 && EmailFormat.TryParse(text.AsSpan(start, end - start), out _))
            {
                matches.Add(new TextMatch(start, end - start));
            }

            at = text.IndexOf('@', Math.Max(end, at + 1));
        }
    }
}
