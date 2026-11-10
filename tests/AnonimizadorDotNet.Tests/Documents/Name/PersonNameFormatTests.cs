using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Documents.Name;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Name;

public sealed class PersonNameFormatTests
{
    [Theory]
    [InlineData("Maria da Silva", "GivenName Particle Surname")]
    [InlineData("João Pedro dos Santos Oliveira", "GivenName GivenName Particle Surname Surname")]
    [InlineData("José Rosa", "GivenName Surname")] // Rosa também é sobrenome
    [InlineData("Carlos Alberto Souza Filho", "GivenName Surname Surname Suffix")] // Alberto fora do dicionário
    [InlineData("Paulo Lima Jr.", "GivenName Surname Suffix")]
    [InlineData("J. Silva", "Initial Surname")]
    [InlineData("Ana M Costa", "GivenName Initial Surname")]
    [InlineData("Maria", "GivenName")]
    [InlineData("Ana Sá-Lima D'Ávila", "GivenName Surname Surname")]
    [InlineData("Lúcia e Silva", "GivenName Particle Surname")]
    public void TryParse_ClassificaAsPartes(string input, string kinds)
    {
        Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? name));

        Assert.Equal(kinds, string.Join(' ', name.Parts.Select(part => part.Kind)));
    }

    [Theory]
    [InlineData("João", "JOAO")]
    [InlineData("JOAO", "JOAO")]
    [InlineData("joão", "JOAO")]
    [InlineData("Conceição", "CONCEICAO")]
    public void TryParse_ChaveIndependeDeCaixaEAcentos(string input, string key)
    {
        Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? name));

        Assert.Equal(key, name.Parts[0].Key);
    }

    [Theory]
    [InlineData("MARIA DA SILVA", "Upper")]
    [InlineData("maria da silva", "Lower")]
    [InlineData("Maria da Silva", "Title")]
    [InlineData("MARIA da silva", "Unknown")]
    public void TryParse_DetectaACaixa(string input, string letterCase)
    {
        Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? name));

        Assert.Equal(Enum.Parse<LetterCase>(letterCase), name.LetterCase);
    }

    [Theory]
    [InlineData("José da Silva", "Accented")]
    [InlineData("JOSE DA SILVA", "Unaccented")]
    [InlineData("Maria Silva", "Unknown")]
    public void TryParse_DetectaOUsoDeAcentos(string input, string accents)
    {
        Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? name));

        Assert.Equal(Enum.Parse<AccentStyle>(accents), name.Accents);
    }

    [Theory]
    [InlineData("José da Silva", "Male")]
    [InlineData("MARIA SOUZA", "Female")]
    [InlineData("Xpto Silva", "Unknown")]
    [InlineData("J. Silva", "Unknown")]
    public void TryParse_DetectaOSexoDoPrenome(string input, string gender)
    {
        Assert.True(PersonNameFormat.TryParse(input, out ParsedPersonName? name));

        Assert.Equal(Enum.Parse<Gender>(gender), name.Gender);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Maria 2 Silva")]
    [InlineData("Maria_Silva")]
    [InlineData("Maria Silva.")] // ponto fora de inicial
    [InlineData("-Maria")]
    [InlineData("Maria Silva-")]
    [InlineData("maria@silva")]
    [InlineData("Maria \uD800 Silva")] // surrogate solto
    public void TryParse_Invalido_DevolveFalso(string input)
    {
        Assert.False(PersonNameFormat.TryParse(input, out ParsedPersonName? name));
        Assert.Null(name);
    }

    [Fact]
    public void TryParse_PrimeiraParte_NuncaEhParticula()
    {
        Assert.True(PersonNameFormat.TryParse("Da Silva", out ParsedPersonName? name));

        Assert.Equal(NamePartKind.GivenName, name.Parts[0].Kind);
    }

    [Fact]
    public void TryParse_LongoDemais_DevolveFalso()
    {
        Assert.False(PersonNameFormat.TryParse(new string('a', PersonNameFormat.MaxLength + 1), out _));
        Assert.False(PersonNameFormat.TryParse(string.Join(' ', Enumerable.Repeat("Silva", PersonNameFormat.MaxParts + 1)), out _));
    }

    [Theory]
    [InlineData("Maria da Silva", "Antônia,Conceição", "Antônia da Conceição")]
    [InlineData("MARIA DA SILVA", "Antônia,Conceição", "ANTÔNIA DA CONCEIÇÃO")]
    [InlineData("maria da silva", "Antônia,Conceição", "antônia da conceição")]
    [InlineData("JOAO DA SILVA", "Antônia,Conceição", "ANTONIA DA CONCEICAO")] // sem acento onde haveria → fonte sem acentos
    [InlineData("  Maria   da Silva ", "Antônia,Conceição", "  Antônia   da Conceição ")]
    [InlineData("MARIA da silva", "Antônia,Conceição", "Antônia da Conceição")] // caixa mista sai em título
    [InlineData("J. Silva", "Maria,Souza", "M. Souza")]
    [InlineData("Ana M Costa", "Rita,Souza,Lima", "Rita S Lima")]
    [InlineData("Paulo Lima Filho", "Mário,Souza", "Mário Souza Filho")]
    [InlineData("PAULO LIMA JR.", "Mário,Souza", "MÁRIO SOUZA JR.")]
    public void Format_ReaplicaPartículasSufixosIniciaisEspacosCaixaEAcentos(string original, string replacements, string expected)
    {
        Assert.True(PersonNameFormat.TryParse(original, out ParsedPersonName? name));

        Assert.Equal(expected, PersonNameFormat.Format(name, replacements.Split(',')));
    }

    [Fact]
    public void Format_QuantidadeErrada_Lanca()
    {
        Assert.True(PersonNameFormat.TryParse("Maria da Silva", out ParsedPersonName? name));

        Assert.Throws<ArgumentException>(() => PersonNameFormat.Format(name, ["Ana"]));
        Assert.Throws<ArgumentException>(() => PersonNameFormat.Format(name, ["Ana", " "]));
    }

    [Fact]
    public void FormatarEInterpretar_MantemONumeroDePartesEACaixa()
    {
        string[] originals = ["Maria da Silva", "JOÃO PEDRO DOS SANTOS", "josé lima filho", "J. Souza", "ANA DE SA"];
        Gen<string> surname = Gen.Int[0, Dictionaries.Surnames.Length - 1].Select(i => Dictionaries.Surnames[i]);
        Gen.Select(Gen.OneOfConst(originals), surname.Array[5]).Sample((original, pool) =>
        {
            Assert.True(PersonNameFormat.TryParse(original, out ParsedPersonName? name));
            string formatted = PersonNameFormat.Format(name, pool.Take(name.ReplaceableCount).ToArray());

            Assert.True(PersonNameFormat.TryParse(formatted, out ParsedPersonName? parsed));
            Assert.Equal(name.Parts.Length, parsed.Parts.Length);
            Assert.Equal(name.LetterCase, parsed.LetterCase);
            Assert.Equal(
                name.Parts.Where(p => !p.IsReplaceable).Select(p => p.Literal),
                parsed.Parts.Where(p => !p.IsReplaceable).Select(p => p.Literal));
            if (name.Accents == AccentStyle.Unaccented)
            {
                Assert.False(AnonimizadorDotNet.Documents.Formatting.TextNormalization.HasDiacritics(formatted));
            }
        });
    }

    [Fact]
    public void TryParse_QualquerTexto_NuncaLanca()
    {
        // Corpo em bloco: com uma expressão booleana, o CsCheck trataria o retorno como a propriedade.
        Gen.String.Sample(input =>
        {
            _ = PersonNameFormat.TryParse(input, out _);
        });
    }

    private static class Dictionaries
    {
        public static readonly string[] Surnames = [.. AnonimizadorDotNet.Resources.EmbeddedLists.Surnames];
    }
}
