using System.Collections.Immutable;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Resources;

namespace AnonimizadorDotNet.Tests.Resources;

public sealed class EmbeddedListsTests
{
    public static TheoryData<string, int> Lists => new()
    {
        { "prenomes-masculinos.txt", 120 },
        { "prenomes-femininos.txt", 120 },
        { "sobrenomes.txt", 200 },
        { "provedores-email.txt", 20 },
        { "sufixos-dominio.txt", 15 },
        { "dominio-inicios.txt", 40 },
        { "dominio-finais.txt", 25 },
    };

    [Theory]
    [MemberData(nameof(Lists))]
    public void Load_TemItensSuficientesSemDuplicatasNemComentarios(string fileName, int minimum)
    {
        ImmutableArray<string> items = EmbeddedLists.Load(fileName);

        Assert.True(items.Length >= minimum, $"{fileName}: {items.Length} itens, mínimo {minimum}.");
        Assert.All(items, item => Assert.False(item.StartsWith('#') || item != item.Trim() || item.Length == 0));
        Assert.Equal(items.Length, items.Select(item => TextNormalization.ToKey(item)).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Load_RecursoInexistente_Lanca() =>
        Assert.Throws<InvalidOperationException>(() => EmbeddedLists.Load("nao-existe.txt"));

    [Fact]
    public void Nomes_EstaoEmTituloSemEspacos()
    {
        IEnumerable<string> names = EmbeddedLists.MaleGivenNames.Concat(EmbeddedLists.FemaleGivenNames).Concat(EmbeddedLists.Surnames);

        Assert.All(names, name =>
        {
            Assert.Equal(LetterCase.Title, LetterCasing.Detect(name));
            Assert.DoesNotContain(' ', name);
        });
    }

    [Fact]
    public void Prenomes_NenhumEstaNosDoisSexos()
    {
        HashSet<string> male = EmbeddedLists.MaleGivenNames.Select(n => TextNormalization.ToKey(n)).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(EmbeddedLists.FemaleGivenNames, name => male.Contains(TextNormalization.ToKey(name)));
    }

    [Fact]
    public void Dominios_SaoMinusculosSemAcentos()
    {
        IEnumerable<string> domains = EmbeddedLists.PublicEmailProviders
            .Concat(EmbeddedLists.DomainSuffixes)
            .Concat(EmbeddedLists.DomainStarts)
            .Concat(EmbeddedLists.DomainEndings);

        Assert.All(domains, domain => Assert.Matches(@"^[a-z0-9]+(\.[a-z0-9]+)*$", domain));
        Assert.All(EmbeddedLists.PublicEmailProviders, domain => Assert.Contains('.', domain));
        Assert.All(EmbeddedLists.DomainSuffixes, suffix => Assert.Contains('.', suffix));
    }

    [Fact]
    public void Provedores_IncluemOsMaisComuns()
    {
        Assert.Contains("gmail.com", (IReadOnlySet<string>)EmbeddedLists.PublicEmailProviders);
        Assert.Contains("hotmail.com", (IReadOnlySet<string>)EmbeddedLists.PublicEmailProviders);
        Assert.Contains("uol.com.br", (IReadOnlySet<string>)EmbeddedLists.PublicEmailProviders);
    }
}
