namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>Padrão de caixa das letras de um valor.</summary>
internal enum LetterCase
{
    /// <summary>Sem letras ou com caixa mista sem padrão reconhecível.</summary>
    Unknown,

    /// <summary>Todas as letras em maiúsculas.</summary>
    Upper,

    /// <summary>Todas as letras em minúsculas.</summary>
    Lower,

    /// <summary>Primeira letra de cada palavra em maiúscula e as demais em minúsculas.</summary>
    Title,
}
