using System.Collections.Immutable;
using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Documents.Name;

/// <summary>Um nome de pessoa já separado em partes, com o estilo de escrita da entrada.</summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class ParsedPersonName
{
    internal ParsedPersonName(
        ImmutableArray<NamePart> parts,
        ImmutableArray<string> separators,
        LetterCase letterCase,
        AccentStyle accents,
        Gender gender)
    {
        Parts = parts;
        Separators = separators;
        LetterCase = letterCase;
        Accents = accents;
        Gender = gender;
        ReplaceableCount = parts.Count(part => part.IsReplaceable);
    }

    /// <summary>As partes, na ordem.</summary>
    public ImmutableArray<NamePart> Parts { get; }

    /// <summary>
    /// Os espaços entre as partes: um antes da primeira, um entre cada par e um depois da última
    /// (<c>Parts.Length + 1</c> itens, podendo ser vazios nas pontas).
    /// </summary>
    public ImmutableArray<string> Separators { get; }

    /// <summary>
    /// Caixa das partes substituídas (partículas não contam). <see cref="LetterCase.Unknown"/> sai em título.
    /// </summary>
    public LetterCase LetterCase { get; }

    /// <summary>Se a fonte escreve com ou sem acentos.</summary>
    public AccentStyle Accents { get; }

    /// <summary>Sexo do primeiro prenome, quando ele está nos dicionários.</summary>
    public Gender Gender { get; }

    /// <summary>Quantidade de partes que recebem substituto.</summary>
    public int ReplaceableCount { get; }
}
