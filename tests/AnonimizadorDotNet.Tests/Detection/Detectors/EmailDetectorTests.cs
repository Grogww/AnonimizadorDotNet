using System.Diagnostics;
using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Detection.Detectors;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection.Detectors;

public sealed class EmailDetectorTests
{
    private static string[] Found(string text) =>
        EmailDetector.Instance.Detect(text).Select(m => text.Substring(m.Index, m.Length)).ToArray();

    [Theory]
    [InlineData("escreva para joao.silva@empresa.com.br hoje", "joao.silva@empresa.com.br")]
    [InlineData("escreva para joao@gmail.com.", "joao@gmail.com")]
    [InlineData("escreva para joao@gmail.com, maria@gmail.com", "joao@gmail.com")]
    [InlineData("(joao@gmail.com)", "joao@gmail.com")]
    [InlineData("<joao@gmail.com>", "joao@gmail.com")]
    [InlineData("mailto:joao@gmail.com", "joao@gmail.com")]
    [InlineData("\"joao@gmail.com\"", "joao@gmail.com")]
    [InlineData("Joao.Silva+boletos@Empresa.COM.BR", "Joao.Silva+boletos@Empresa.COM.BR")]
    [InlineData("..joao@gmail.com", "joao@gmail.com")]
    [InlineData("joao@empresa.com.br-", "joao@empresa.com.br")]
    [InlineData("contato:joao_87@empresa-x.com.br;", "joao_87@empresa-x.com.br")]
    public void Detecta_SemPontuacaoAoRedor(string text, string expected) => Assert.Equal(expected, Found(text)[0]);

    [Fact]
    public void Detecta_VariasOcorrencias()
    {
        Assert.Equal(
            ["a@gmail.com", "b.c@empresa.com.br", "d@x.io"],
            Found("de a@gmail.com para b.c@empresa.com.br, cópia d@x.io."));
    }

    [Theory]
    [InlineData("sem arroba")]
    [InlineData("@gmail.com")]
    [InlineData("joao@")]
    [InlineData("joao@localhost")]
    [InlineData("joao@empresa.123")]
    [InlineData("joao..silva@gmail.com")]
    [InlineData("usuário @ empresa . com")]
    [InlineData("@@@")]
    public void NaoDetecta(string text) => Assert.Empty(Found(text));

    [Fact]
    public void TextoLongoSemArroba_ELinear()
    {
        string text = string.Concat(Enumerable.Repeat("a.", 500_000));
        var watch = Stopwatch.StartNew();
        Assert.Empty(Found(text));
        Assert.Empty(Found(text + "@" + text));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "A varredura de e-mail deve ser linear.");
    }

    [Fact]
    public void Propriedade_EmailEmbutido_EDetectadoNaPosicaoExata()
    {
        Gen<string> words = Gen.Char["abcdefghijklmnopqrstuvwxyz0123456789"].Array[1, 10].Select(c => new string(c));
        Gen.Select(words, words, Gen.Int[0, 3], words).Sample((local, domain, provider, suffix) =>
        {
            string email = provider switch
            {
                0 => $"{local}@gmail.com",
                1 => $"{local}.{suffix}@{domain}.com.br",
                2 => $"{local}@{domain}.gov.br",
                _ => $"{local}_{suffix}@{domain}.org",
            };

            string text = $"envie para {email}, obrigado";
            Assert.Equal([new TextMatch(11, email.Length)], EmailDetector.Instance.Detect(text));
        });
    }

    [Fact]
    public void Propriedade_NuncaLanca()
    {
        Gen.String.Sample(text =>
        {
            _ = EmailDetector.Instance.Detect(text).ToArray();
        });
    }
}
