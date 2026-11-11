using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Documents.Name;
using AnonimizadorDotNet.Resources;
using AnonimizadorDotNet.Transformation.Name;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Transformation.Name;

public sealed class NameTransformerTests : IDisposable
{
    private static readonly HashSet<string> MaleKeys = Keys(EmbeddedLists.MaleGivenNames);
    private static readonly HashSet<string> FemaleKeys = Keys(EmbeddedLists.FemaleGivenNames);
    private static readonly HashSet<string> SurnameKeys = Keys(EmbeddedLists.Surnames);

    private readonly KeyRing _keys = new(ReferenceSubstitutes.MasterKey);
    private readonly NameTransformer _transformer;

    public NameTransformerTests() => _transformer = new NameTransformer(_keys);

    /// <summary>Nomes montados a partir dos dicionários, com partículas, sufixos e caixas variadas.</summary>
    private static readonly Gen<string> Names =
        from given in Gen.OneOfConst(EmbeddedLists.MaleGivenNames.Concat(EmbeddedLists.FemaleGivenNames).ToArray())
        from surnames in Gen.OneOfConst(EmbeddedLists.Surnames.ToArray()).Array[1, 3]
        from particle in Gen.OneOfConst("", "da ", "dos ")
        from suffix in Gen.OneOfConst("", " Filho", " Neto")
        from casing in Gen.Int[0, 2]
        let name = $"{given} {particle}{string.Join(' ', surnames)}{suffix}"
        select casing switch
        {
            0 => name,
            1 => name.ToUpperInvariant(),
            _ => name.ToLowerInvariant(),
        };

    public void Dispose()
    {
        _transformer.Dispose();
        _keys.Dispose();
    }

    /// <summary>Valores dourados: fixam rótulo, contextos e a chave (nome completo normalizado).</summary>
    [Theory]
    [InlineData("Maria da Silva", "Luana da Amaral")]
    [InlineData("JOÃO PEDRO SOUZA FILHO", "HEITOR SEBASTIÃO CONCEIÇÃO FILHO")]
    [InlineData("ana paula ferreira", "agnes nathália pinheiro")]
    [InlineData("J. Silva", "R. Pontes")]
    [InlineData("Xpto Silva", "Kátia Novaes")]
    public void TryTransform_ChaveConhecida_ProduzSubstitutoFixo(string input, string expected)
    {
        Assert.True(_transformer.TryTransform(input, out string? substitute));

        Assert.Equal(expected, substitute);
    }

    [Fact]
    public void TryTransform_MesmoNomeEmGrafiasDiferentes_MesmoSubstitutoNaGrafiaDeCada()
    {
        Assert.True(_transformer.TryTransform("José da Conceição", out string? title));
        Assert.True(_transformer.TryTransform("JOSE DA CONCEICAO", out string? upper));
        Assert.True(_transformer.TryTransform("  josé   da conceição ", out string? lower));

        Assert.Equal(TextNormalization.ToKey(title), TextNormalization.ToKey(upper));
        Assert.Equal(TextNormalization.ToKey(title), TextNormalization.ToKey(lower.Trim()).Replace("   ", " ", StringComparison.Ordinal));
        Assert.Equal(upper.ToUpperInvariant(), upper);
        Assert.False(TextNormalization.HasDiacritics(upper));
        Assert.StartsWith("  ", lower, StringComparison.Ordinal);
        Assert.Equal(lower.ToLowerInvariant(), lower);
    }

    [Fact]
    public void TryTransform_PreservaEstruturaECaixa()
    {
        Names.Sample(input =>
        {
            Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? original));
            Assert.True(_transformer.TryTransform(input, out string? substitute));
            Assert.True(PersonNameFormat.TryParse(substitute, out ParsedPersonName? parsed));

            Assert.Equal(original.Parts.Length, parsed.Parts.Length);
            Assert.Equal(LetterCasing.Detect(input), LetterCasing.Detect(substitute));
            for (int i = 0; i < original.Parts.Length; i++)
            {
                if (!original.Parts[i].IsReplaceable)
                {
                    Assert.Equal(original.Parts[i].Literal, parsed.Parts[i].Literal);
                }
            }
        });
    }

    [Fact]
    public void TryTransform_PrenomeConhecido_MantemOSexo()
    {
        Names.Sample(input =>
        {
            Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? original));
            Assert.True(_transformer.TryTransform(input, out string? substitute));
            string first = TextNormalization.ToKey(substitute.Trim().Split(' ')[0]);

            switch (original.Gender)
            {
                case Gender.Male:
                    Assert.Contains(first, MaleKeys);
                    break;
                case Gender.Female:
                    Assert.Contains(first, FemaleKeys);
                    break;
                default:
                    Assert.True(MaleKeys.Contains(first) || FemaleKeys.Contains(first));
                    break;
            }
        });
    }

    [Fact]
    public void TryTransform_SobrenomesSaemDoDicionario()
    {
        Names.Sample(input =>
        {
            Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? original));
            Assert.True(_transformer.TryTransform(input, out string? substitute));
            string[] parts = substitute.Trim().Split(' ');

            for (int i = 0; i < original.Parts.Length; i++)
            {
                if (original.Parts[i].Kind == NamePartKind.Surname)
                {
                    Assert.Contains(TextNormalization.ToKey(parts[i]), SurnameKeys);
                }
            }
        });
    }

    [Fact]
    public void TryTransform_ChaveEONomeCompleto_SobrenomeComumNaoRevelaParentesco()
    {
        var surnames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string given in EmbeddedLists.FemaleGivenNames.Take(40))
        {
            Assert.True(_transformer.TryTransform($"{given} Silva", out string? substitute));
            surnames.Add(substitute.Split(' ')[^1]);
        }

        Assert.True(surnames.Count > 20, $"Só {surnames.Count} sobrenomes distintos em 40 nomes.");
    }

    [Fact]
    public void TryTransform_OutraChave_OutroSubstituto()
    {
        byte[] otherKey = ReferenceSubstitutes.MasterKey.Select(b => (byte)(b ^ 0xFF)).ToArray();
        using var otherKeys = new KeyRing(otherKey);
        using var other = new NameTransformer(otherKeys);

        int equal = 0;
        foreach (string given in EmbeddedLists.MaleGivenNames.Take(50))
        {
            Assert.True(_transformer.TryTransform($"{given} Souza Lima", out string? first));
            Assert.True(other.TryTransform($"{given} Souza Lima", out string? second));
            equal += first == second ? 1 : 0;
        }

        Assert.True(equal <= 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Maria 2")]
    public void TryTransform_Invalido_DevolveFalso(string input) =>
        Assert.False(_transformer.TryTransform(input, out _));

    [Fact]
    public void TryTransform_QualquerTexto_NuncaLanca() =>
        Gen.String.Sample(input => { _transformer.TryTransform(input, out _); });

    [Fact]
    public void DepoisDoDescarte_Lanca()
    {
        var transformer = new NameTransformer(_keys);
        transformer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transformer.TryTransform("Maria Silva", out _));
    }

    private static HashSet<string> Keys(IEnumerable<string> items) =>
        items.Select(item => TextNormalization.ToKey(item)).ToHashSet(StringComparer.Ordinal);
}
