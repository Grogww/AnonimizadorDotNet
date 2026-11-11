namespace AnonimizadorDotNet;

/// <summary>Como um valor chegou à biblioteca: como texto ou como número.</summary>
/// <remarks>
/// A distinção importa para documentos guardados em campos numéricos: o CPF <c>012.345.678-90</c> num campo
/// inteiro vira <c>12345678909</c>, sem o zero à esquerda. Um valor que chegou como número sai como número,
/// também sem zeros à esquerda; um que chegou como texto sai como texto, na mesma máscara.
/// </remarks>
public enum ValueKind
{
    /// <summary>Texto, com ou sem máscara; zeros à esquerda são significativos.</summary>
    Text,

    /// <summary>Número inteiro: sem separadores e sem zeros à esquerda, na entrada e na saída.</summary>
    Number,
}
