using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Tests.Documents.Formatting;

public sealed class LetterCasingTests
{
    [Theory]
    [InlineData("MARIA DA SILVA", "Upper")]
    [InlineData("maria da silva", "Lower")]
    [InlineData("Maria Silva", "Title")]
    [InlineData("João D'Ávila Sá-Lima", "Title")]
    [InlineData("Maria da Silva", "Unknown")]
    [InlineData("mARIA", "Unknown")]
    [InlineData("12ABC345", "Upper")]
    [InlineData("12abc345", "Lower")]
    [InlineData("12345", "Unknown")]
    [InlineData("", "Unknown")]
    public void Detect_ReconheceOPadrao(string text, string expected) =>
        Assert.Equal(Enum.Parse<LetterCase>(expected), LetterCasing.Detect(text));

    [Theory]
    [InlineData("josé d'ávila sá-lima", "Title", "José D'Ávila Sá-Lima")]
    [InlineData("José Ávila", "Upper", "JOSÉ ÁVILA")]
    [InlineData("JOSÉ ÁVILA", "Lower", "josé ávila")]
    [InlineData("jOsÉ", "Unknown", "jOsÉ")]
    [InlineData("12abc", "Upper", "12ABC")]
    public void Apply_AplicaOPadrao(string text, string letterCase, string expected) =>
        Assert.Equal(expected, LetterCasing.Apply(text, Enum.Parse<LetterCase>(letterCase)));
}
