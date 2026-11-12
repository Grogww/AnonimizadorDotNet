using System.Text.RegularExpressions;

namespace AnonimizadorDotNet.Detection.Detectors;

/// <summary>Base dos detectores embutidos: um padrão de forma seguido da validação do tipo.</summary>
/// <remarks>
/// Só devolve trechos que o <c>XxxFormat.TryParse</c> do tipo aceita, para que um número qualquer (dígito
/// verificador errado, DDD inexistente) não dispute posição com um trecho válido de outro tipo.
/// </remarks>
internal abstract class PatternDetector : ITextDetector
{
    /// <summary>Valida a forma encontrada pelo padrão.</summary>
    protected delegate bool Validator(ReadOnlySpan<char> candidate);

    /// <inheritdoc/>
    public abstract string Type { get; }

    /// <inheritdoc/>
    public IEnumerable<TextMatch> Detect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var matches = new List<TextMatch>();
        Collect(text, matches);
        return matches;
    }

    /// <summary>Acrescenta os trechos válidos do texto a uma lista.</summary>
    public abstract void Collect(string text, List<TextMatch> matches);

    /// <summary>Acrescenta os casamentos de um padrão que passam na validação.</summary>
    protected static void Collect(Regex pattern, string text, List<TextMatch> matches, Validator isValid)
    {
        foreach (ValueMatch match in pattern.EnumerateMatches(text))
        {
            if (isValid(text.AsSpan(match.Index, match.Length)))
            {
                matches.Add(new TextMatch(match.Index, match.Length));
            }
        }
    }
}
