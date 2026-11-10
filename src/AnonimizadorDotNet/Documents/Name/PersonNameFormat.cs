using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Resources;

namespace AnonimizadorDotNet.Documents.Name;

/// <summary>Separação e remontagem de nomes de pessoa.</summary>
/// <remarks>
/// <para>
/// O nome é dividido em partes por espaços. A primeira parte é o prenome; as seguintes são sobrenomes,
/// exceto quando um prenome conhecido (e que não é também sobrenome) vem logo depois do primeiro, como em
/// "João Pedro Silva". Partículas ("da", "de", "do", "das", "dos", "e") e sufixos de parentesco ("Filho",
/// "Júnior", "Neto"…) são preservados como estão; iniciais ("J." ou "J") viram outra inicial.
/// </para>
/// <para>
/// A saída mantém o número de partes, os espaços, a caixa (maiúsculas, minúsculas ou título) e o uso de
/// acentos: se a entrada não tem acento em nenhuma parte, mas alguma normalmente teria ("JOAO"), a fonte
/// remove acentos e o substituto sai sem eles.
/// </para>
/// </remarks>
internal static class PersonNameFormat
{
    /// <summary>Tamanho máximo aceito, em caracteres.</summary>
    public const int MaxLength = 200;

    /// <summary>Quantidade máxima de partes.</summary>
    public const int MaxParts = 15;

    private static readonly FrozenSet<string> Particles =
        new[] { "DA", "DE", "DO", "DAS", "DOS", "E" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> Suffixes =
        new[] { "FILHO", "FILHA", "JUNIOR", "JR", "NETO", "NETA", "SOBRINHO", "SOBRINHA" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly Lazy<Lookup> SharedLookup = new(() => new Lookup());

    /// <summary>Separa um nome de pessoa em partes.</summary>
    /// <param name="input">O nome como chegou.</param>
    /// <param name="name">O nome separado.</param>
    /// <returns>Falso se houver caractere que não é letra, espaço, hífen, apóstrofo ou ponto de inicial, se não
    /// houver parte a substituir ou se o nome for longo demais. Nunca lança exceção por causa do conteúdo.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ParsedPersonName? name)
    {
        name = null;
        if (input.IsEmpty || input.Length > MaxLength)
        {
            return false;
        }

        // Surrogates soltos não são texto válido (e fariam a normalização Unicode lançar exceção).
        foreach (char c in input)
        {
            if (char.IsSurrogate(c))
            {
                return false;
            }
        }

        string text = new string(input).Normalize(NormalizationForm.FormC);
        if (!TrySplit(text, out List<string> tokens, out List<string> separators))
        {
            return false;
        }

        Lookup lookup = SharedLookup.Value;
        ImmutableArray<NamePart>.Builder parts = ImmutableArray.CreateBuilder<NamePart>(tokens.Count);
        var replaceableText = new StringBuilder();
        bool hasAccent = false;
        bool normallyAccented = false;
        bool lastWasGivenName = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            if (!TryClassify(tokens[i], isFirst: parts.Count == 0, lastWasGivenName, lookup, out NamePart? part))
            {
                return false;
            }

            parts.Add(part);
            lastWasGivenName = part.Kind == NamePartKind.GivenName;
            if (part.IsReplaceable)
            {
                replaceableText.Append(tokens[i]).Append(' ');
                hasAccent |= TextNormalization.HasDiacritics(tokens[i]);
                normallyAccented |= lookup.AccentedKeys.Contains(part.Key);
            }
        }

        if (!parts.Any(part => part.IsReplaceable))
        {
            return false;
        }

        AccentStyle accents = hasAccent ? AccentStyle.Accented
            : normallyAccented ? AccentStyle.Unaccented
            : AccentStyle.Unknown;

        name = new ParsedPersonName(
            parts.ToImmutable(),
            [.. separators],
            LetterCasing.Detect(replaceableText.ToString()),
            accents,
            GenderOf(parts, lookup));
        return true;
    }

    /// <summary>Remonta o nome com substitutos para as partes substituíveis, no estilo do original.</summary>
    /// <param name="original">O nome cujas partículas, sufixos, espaços, caixa e acentos são reaplicados.</param>
    /// <param name="replacements">
    /// Um substituto por parte substituível, na ordem (na grafia do dicionário, com acentos). Para uma inicial,
    /// só a primeira letra do substituto é usada.
    /// </param>
    /// <exception cref="ArgumentException">A quantidade de substitutos não confere, ou algum é vazio.</exception>
    public static string Format(ParsedPersonName original, IReadOnlyList<string> replacements)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Count != original.ReplaceableCount)
        {
            throw new ArgumentException(
                $"O nome tem {original.ReplaceableCount} partes substituíveis, mas recebeu {replacements.Count} substitutos.",
                nameof(replacements));
        }

        LetterCase letterCase = original.LetterCase == LetterCase.Unknown ? LetterCase.Title : original.LetterCase;
        var builder = new StringBuilder();
        int next = 0;
        for (int i = 0; i < original.Parts.Length; i++)
        {
            builder.Append(original.Separators[i]);
            NamePart part = original.Parts[i];
            if (!part.IsReplaceable)
            {
                builder.Append(part.Literal);
                continue;
            }

            string replacement = replacements[next++];
            if (string.IsNullOrWhiteSpace(replacement))
            {
                throw new ArgumentException("Os substitutos não podem ser vazios.", nameof(replacements));
            }

            if (part.Kind == NamePartKind.Initial)
            {
                replacement = replacement[..1] + (part.HasPeriod ? "." : string.Empty);
            }

            if (original.Accents == AccentStyle.Unaccented)
            {
                replacement = TextNormalization.RemoveDiacritics(replacement);
            }

            builder.Append(LetterCasing.Apply(replacement, letterCase));
        }

        builder.Append(original.Separators[^1]);
        return builder.ToString();
    }

    private static bool TrySplit(string text, out List<string> tokens, out List<string> separators)
    {
        tokens = [];
        separators = [];
        int position = 0;
        while (true)
        {
            int start = position;
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            separators.Add(text[start..position]);
            if (position == text.Length)
            {
                return tokens.Count > 0;
            }

            start = position;
            while (position < text.Length && !char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            tokens.Add(text[start..position]);
            if (tokens.Count > MaxParts)
            {
                return false;
            }
        }
    }

    private static bool TryClassify(string token, bool isFirst, bool lastWasGivenName, Lookup lookup, [NotNullWhen(true)] out NamePart? part)
    {
        part = null;
        bool hasPeriod = token.EndsWith('.');
        string body = hasPeriod ? token[..^1] : token;
        if (body.Length == 0 || !char.IsLetter(body[0]) || !char.IsLetter(body[^1]))
        {
            return false;
        }

        foreach (char c in body)
        {
            if (!char.IsLetter(c) && c is not ('-' or '\'' or '’'))
            {
                return false;
            }
        }

        string key = TextNormalization.ToKey(body);
        if (!isFirst && !hasPeriod && Particles.Contains(key))
        {
            part = new NamePart(NamePartKind.Particle, key, token, hasPeriod);
            return true;
        }

        if (body.Length == 1)
        {
            part = new NamePart(NamePartKind.Initial, key, literal: null, hasPeriod);
            return true;
        }

        // Só uma inicial pode terminar em ponto, exceto a abreviação "Jr.".
        if (hasPeriod && key != "JR")
        {
            return false;
        }

        if (!isFirst && Suffixes.Contains(key))
        {
            part = new NamePart(NamePartKind.Suffix, key, token, hasPeriod);
            return true;
        }

        bool givenName = isFirst
            || (lastWasGivenName && lookup.GivenNameKeys.Contains(key) && !lookup.SurnameKeys.Contains(key));
        part = new NamePart(givenName ? NamePartKind.GivenName : NamePartKind.Surname, key, literal: null, hasPeriod: false);
        return true;
    }

    private static Gender GenderOf(ImmutableArray<NamePart>.Builder parts, Lookup lookup)
    {
        NamePart? first = parts.FirstOrDefault(part => part.Kind == NamePartKind.GivenName);
        if (first is null)
        {
            return Gender.Unknown;
        }

        return lookup.MaleKeys.Contains(first.Key) ? Gender.Male
            : lookup.FemaleKeys.Contains(first.Key) ? Gender.Female
            : Gender.Unknown;
    }

    /// <summary>Os dicionários indexados pela chave normalizada.</summary>
    private sealed class Lookup
    {
        public Lookup()
        {
            MaleKeys = Keys(EmbeddedLists.MaleGivenNames);
            FemaleKeys = Keys(EmbeddedLists.FemaleGivenNames);
            SurnameKeys = Keys(EmbeddedLists.Surnames);
            GivenNameKeys = MaleKeys.Union(FemaleKeys).ToFrozenSet(StringComparer.Ordinal);
            AccentedKeys = EmbeddedLists.MaleGivenNames
                .Concat(EmbeddedLists.FemaleGivenNames)
                .Concat(EmbeddedLists.Surnames)
                .Where(item => TextNormalization.HasDiacritics(item))
                .Select(item => TextNormalization.ToKey(item))
                .ToFrozenSet(StringComparer.Ordinal);
        }

        public FrozenSet<string> MaleKeys { get; }

        public FrozenSet<string> FemaleKeys { get; }

        public FrozenSet<string> GivenNameKeys { get; }

        public FrozenSet<string> SurnameKeys { get; }

        public FrozenSet<string> AccentedKeys { get; }

        private static FrozenSet<string> Keys(ImmutableArray<string> items) =>
            items.Select(item => TextNormalization.ToKey(item)).ToFrozenSet(StringComparer.Ordinal);
    }
}
