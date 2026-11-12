using System.Text.RegularExpressions;
using AnonimizadorDotNet.Detection.Patterns;
using AnonimizadorDotNet.Documents.Phone;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Detection.Detectors;

/// <summary>Encontra telefones brasileiros com DDD válido.</summary>
/// <remarks>
/// <para>
/// O DDD é obrigatório: fixo com 10 dígitos e celular com 11, opcionalmente precedidos de <c>+55</c> ou
/// <c>55</c>. O assinante sem DDD (8 ou 9 dígitos soltos) e o prefixo <c>0</c> de longa distância não são
/// detectados: qualquer número de 8 dígitos viraria telefone.
/// </para>
/// <para>
/// Sem separador nenhum no número nacional, só o celular de 11 dígitos (DDD + 9) é aceito; o fixo de
/// 10 dígitos colado exige máscara, parênteses, espaço ou hífen, porque 10 dígitos seguidos são comuns em
/// protocolos e códigos.
/// </para>
/// </remarks>
internal sealed class PhoneDetector : PatternDetector
{
    private const int LandlineDigits = 10;

    private PhoneDetector()
    {
    }

    /// <summary>A instância única (o detector não tem estado).</summary>
    public static PhoneDetector Instance { get; } = new();

    /// <inheritdoc/>
    public override string Type => DocumentTypeNames.Phone;

    /// <inheritdoc/>
    public override void Collect(string text, List<TextMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        // A regra do número colado depende do grupo nacional, que o EnumerateMatches não expõe.
        for (Match match = DocumentPatterns.Phone().Match(text); match.Success; match = match.NextMatch())
        {
            ReadOnlySpan<char> national = match.Groups["national"].ValueSpan;
            bool bare = !national.ContainsAnyExceptInRange('0', '9');
            if (bare && national.Length == LandlineDigits)
            {
                continue;
            }

            if (PhoneFormat.TryParse(match.ValueSpan, ValueKind.Text, out _))
            {
                matches.Add(new TextMatch(match.Index, match.Length));
            }
        }
    }
}
