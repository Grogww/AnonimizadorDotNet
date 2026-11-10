namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>Como o valor chegou: como texto ou como número.</summary>
internal enum ValueKind
{
    /// <summary>Texto, com ou sem máscara; zeros à esquerda são significativos.</summary>
    Text,

    /// <summary>Número inteiro: sem separadores e sem zeros à esquerda, na entrada e na saída.</summary>
    Number,
}
