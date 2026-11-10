using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Tests.Documents.Formatting;

public sealed class TextNormalizationTests
{
    [Theory]
    [InlineData("João", "JOAO")]
    [InlineData("JOAO", "JOAO")]
    [InlineData("joão", "JOAO")]
    [InlineData("Gonçalves", "GONCALVES")]
    [InlineData("Ávila", "AVILA")]
    [InlineData("Uchôa", "UCHOA")]
    public void ToKey_IgnoraCaixaEAcentos(string text, string expected) =>
        Assert.Equal(expected, TextNormalization.ToKey(text));

    [Fact]
    public void ToKey_FormaDecomposta_TemAMesmaChave() =>
        Assert.Equal(TextNormalization.ToKey("João"), TextNormalization.ToKey("João"));

    [Theory]
    [InlineData("José", true)]
    [InlineData("Conceição", true)]
    [InlineData("Jose", false)]
    [InlineData("MARIA DA SILVA", false)]
    [InlineData("", false)]
    public void HasDiacritics_DetectaAcentos(string text, bool expected) =>
        Assert.Equal(expected, TextNormalization.HasDiacritics(text));

    [Fact]
    public void RemoveDiacritics_MantemACaixa() =>
        Assert.Equal("Sebastiao Conceicao", TextNormalization.RemoveDiacritics("Sebastião Conceição"));
}
