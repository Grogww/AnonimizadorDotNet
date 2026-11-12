using AnonimizadorDotNet.Detection.Patterns;
using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Detection.Detectors;

/// <summary>Encontra CNPJs numéricos e alfanuméricos com dígitos verificadores válidos, com ou sem máscara.</summary>
/// <remarks>
/// O CNPJ alfanumérico sem máscara tem a forma de muitos códigos (14 posições terminando em 2 dígitos); só
/// os de verificadores corretos são aceitos, o que deixa passar cerca de 1 em cada 100 códigos com essa forma.
/// </remarks>
internal sealed class CnpjDetector : PatternDetector
{
    private CnpjDetector()
    {
    }

    /// <summary>A instância única (o detector não tem estado).</summary>
    public static CnpjDetector Instance { get; } = new();

    /// <inheritdoc/>
    public override string Type => DocumentTypeNames.Cnpj;

    /// <inheritdoc/>
    public override void Collect(string text, List<TextMatch> matches)
    {
        Collect(DocumentPatterns.NumericCnpj(), text, matches, static candidate => IsValid(candidate));

        // O padrão alfanumérico também casa os só de dígitos, já cobertos acima (com limites mais largos).
        Collect(
            DocumentPatterns.AlphanumericCnpj(),
            text,
            matches,
            static candidate => (candidate.ContainsAnyInRange('A', 'Z') || candidate.ContainsAnyInRange('a', 'z'))
                && IsValid(candidate));
    }

    private static bool IsValid(ReadOnlySpan<char> candidate) => CnpjFormat.TryParse(candidate, ValueKind.Text, out _);
}
